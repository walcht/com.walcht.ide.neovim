#pragma warning disable IDE0130
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR_WIN
using System.IO.Pipes;
using System.Runtime.InteropServices;
#endif


namespace Neovim.Editor
{
  [InitializeOnLoad]
  public class NeovimCodeEditor : IExternalCodeEditor
  {
    static readonly string[] _supportedFileNames = { "nvim", "nvim.exe" };
    static bool s_WindowFocusingAvailable = false;

    public static NeovimEditorConfig s_Config = new();

    // Unique identifier for this Unity instance (PID)
    public static readonly string s_InstanceId = Process.GetCurrentProcess().Id.ToString();

#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
    static string s_ServerSocket = $"/tmp/nvimsocket_{Process.GetCurrentProcess().Id}";
#else // UNITY_EDITOR_WIN
    // this is initialized to some "127.0.0.1:<random-port>" because Unix domain sockets on Windows are a bitch
    // on Windows, listening to a domain socket yields the following error: "neovim Failed to --listen: service not
    // available for socket type" so we have to listen to a TCP socket instead with a local addr and a random port
    static string s_ServerSocket = $"127.0.0.1:{NetUtils.GetRandomAvailablePort()}";
    public static readonly string s_GetProcessPPIDPath = Path.GetFullPath("Packages/com.walcht.ide.neovim/GetProcessPPID.ps1");
#endif

    public static string ServerSocket
    {
      get => s_ServerSocket;
    }

    /// <summary>
    ///   These are the default template arguments that one of which can potentially be used
    ///   to send request to the Neovim server instance upon opening a file (or clicking on
    ///   error message in console, etc). Depending on the modifier that is currently applied,
    ///   different commands could be sent to the Neovim server instance (e.g., open in a new
    ///   tab, or open in a vertical split, etc.). First entry is the default.
    /// </summary>
    public static readonly (string Args, string Name, string Desc)[] s_OpenFileArgsTemplates = {
      ("--server {serverSocket} --remote-tab {filePath}",
       "Open in new tab",
       "Always opens the file in a new Neovim tab page."),
      ("--server {serverSocket} --remote-send \":drop {filePath}<CR>\"",
       "Open (reuse window)",
       "Opens in current window. If file is already open somewhere — switches to it. No new tabs."),
      ("--server {serverSocket} --remote-send \":vsplit {filePath}<CR>\"",
       "Vertical split",
       "Opens the file in a vertical split of the current window."),
      ("--server {serverSocket} --remote-send \":split {filePath}<CR>\"",
       "Horizontal split",
       "Opens the file in a horizontal split of the current window."),
    };

    /// <summary>
    ///   These are the default template arguments that one of which can potentially be used
    ///   to send request to the Neovim server instance to jump to a given cursor position.
    ///   First entry is the default.
    /// </summary>
    public static readonly (string Args, string Name, string Desc)[] s_JumpToCursorPositionArgsTemplates = {
      ("--server {serverSocket} --remote-send \":call cursor({line},{column})<CR>\"",
       "Jump to position via cursor call",
       "Jumps to requested position in the current buffer using nvim lua cursor call."),
    };

    // add your file extension here if you want it to be opened by Neovim via Unity
    static readonly string[] s_SupportedExtensions = {
      // csharp
      "cs",
      "csproj",
      "sln",
      // python
      "py",
      // shader files
      "shader",
      "cginc",
      // misc
      "txt",
      "json",
      "yaml",
      "xml",
    };

#if UNITY_EDITOR_LINUX
    private enum LinuxDesktopEnvironment
    {
      X11, // if we are on X11 - wmctrl solves our window focusing issues
      GNOME,  // GNOME (e.g., Ubuntu) on Wayland
      KDE,  // KDE on Wayland
      OTHER,
      UNKNOWN,  // can't be determined :/
    }
    private static readonly LinuxDesktopEnvironment s_LinuxPlatform;
#elif UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
#endif

    // terminal launch command template - use this template for adding new launch cmds
    public static readonly (string, string) s_TermLaunchCmdTemplate = ("<terminal-emulator>", "--title \"nvimunity-{instanceId}\" -- {app} {filePath} --listen {serverSocket}");

    // list of neovim launch cmds from popular terminal emulators - this is
    // just a hardcoded list so that non-tech-savy users can just get to
    // using Neovim with minimal friction.
    public static readonly (string, string)[] s_TermLaunchCmds =
#if UNITY_EDITOR_LINUX
    {
      ("gnome-terminal", "--title \"nvimunity-{instanceId}\" -- {app} {filePath} --listen {serverSocket}"),
      ("alacritty", "--title \"nvimunity-{instanceId}\" --command {app} {filePath} --listen {serverSocket}"),
      ("ptyxis", "--title \"nvimunity-{instanceId}\" -- {app} {filePath} --listen {serverSocket}"),
      ("xterm", "-T \"nvimunity-{instanceId}\" -e {app} {filePath} --listen {serverSocket}"),
      ("ghostty", "--title=\"nvimunity-{instanceId}\" --command='{app} {filePath} --listen {serverSocket}'"),
    };
#elif UNITY_EDITOR_OSX
    {
      ("/Applications/kitty.app/Contents/MacOS/kitty", "--title \"nvimunity-{instanceId}\" {app} {filePath} --listen {serverSocket}"),
      ("/Applications/Alacritty.app/Contents/MacOS/alacritty", "--title \"nvimunity-{instanceId}\" --command {app} {filePath} --listen {serverSocket}"),
      ("/Applications/ghostty.app/Contents/MacOS/ghostty", "--title=\"nvimunity-{instanceId}\" --command='{app} {filePath} --listen {serverSocket}'"),
      ("/Applications/iTerm.app/Contents/MacOS/iTerm2", "--title \"nvimunity-{instanceId}\" -- {app} {filePath} --listen {serverSocket}"),
      ("alacritty", "--title \"nvimunity-{instanceId}\" --command {app} {filePath} --listen {serverSocket}"),
      ("ghostty", "--title=\"nvimunity-{instanceId}\" --command='{app} {filePath} --listen {serverSocket}'"),
      ("kitty", "--title \"nvimunity-{instanceId}\" {app} {filePath} --listen {serverSocket}"),
    };
#else  // UNITY_EDITOR_WIN
    {
      // on Powershell, replace the ';' with "`;"
      ("wt", "nt {app} {filePath} --listen {serverSocket} ; nt Powershell -File {getProcessPPIDScriptPath}"),
      ("alacritty", "--title \"nvimunity-{instanceId}\" --command {app} {filePath} --listen {serverSocket}")
    };
#endif

    // Fallback Neovim installation paths (only used in case nvim executable path is not explicitly provided). The first
    // valid path is picked. "nvim(.exe)" is a special case where PATH is checked for its existence.
    private static readonly string[] s_CandidateNeovimPaths =
#if UNITY_EDITOR_LINUX
     {
       "nvim",
       "/usr/bin/nvim",
       "/opt/nvim-linux64/bin/nvim",
       "/opt/nvim-linux-x86_64/bin/nvim",
     };
#elif UNITY_EDITOR_OSX
     {
       "nvim",
       "/usr/local/bin/nvim",
       "/opt/homebrew/bin/nvim",
       "/usr/bin/nvim",
     };
#else // UNITY_EDITOR_WIN
     // make sure to include the extension in the executalbe's name!
     {
       "nvim.exe",  // powershell bitches about missing .exe extension
       Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Path.Join("Neovim", "bin", "nvim.exe")),
     };
#endif

    private static IGenerator s_Generator = null;

    /// <summary>
    /// Sets the default terminal launch command, terminal launch arguments, open-file request arguments,
    /// and jump-to-cursor-position request arguments in case any of them is null/not already set.
    /// </summary>
    private static bool SetDefaults()
    {
      if (s_DiscoveredNeovimInstallations.Any())
      {
        s_Config.NvimExecutablePath = s_DiscoveredNeovimInstallations.First().Path;
      }

      string termLaunchCmd = s_Config.TermLaunchCmd;
      string termLaunchArgs = s_Config.TermLaunchArgs;

      // if cmd is empty/whitespace => no terminal launch cmd has been provided/chosen yet
      if (string.IsNullOrWhiteSpace(termLaunchCmd) || string.IsNullOrWhiteSpace(termLaunchArgs))
      {
        // pick the first default available terminal from the list of 'popular' terminal emulators. Obviously this is
        // some sort of a heuristic but the user can explicitly change this through the GUI.
        bool s = false;
        foreach (var termLaunch in s_TermLaunchCmds)
        {
          if (TryChangeTermLaunchCmd(termLaunch.Item1, termLaunch.Item2))
          {
            s = true;
            break;
          }
        }
        // no available terminal is found from the 'most common' term list
        if (!s)
        {
          // you can't show a GUI window here -- so just log a warning
          Debug.LogError($"[neovim.ide] no valid terminal launcher is available. " +
              "You have to set the terminal launch command by going to the menu item: Neovim => Settings");
          // TODO: open settings menu
          return false;
        }
      }

      if (!s_Config.ModifierBindings.Any() && !string.IsNullOrWhiteSpace(s_Config.OpenFileArgs))
      {
        s_Config.ModifierBindings.Add(new ModifierBinding { Modifiers = 0, Args = s_Config.OpenFileArgs });
        s_Config.SetDirty(true);
        s_Config.Save();
      }

      if (!s_Config.ModifierBindings.Any())
      {
        if (!s_OpenFileArgsTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] open-file template list is empty");
        }
        s_Config.ModifierBindings = new System.Collections.Generic.List<ModifierBinding> {
          new() { Modifiers = 0, Args = s_OpenFileArgsTemplates[0].Args }
        };
        s_Config.Save();
      }

      if (string.IsNullOrWhiteSpace(s_Config.JumpToCursorPositionArgs))
      {
        if (!s_JumpToCursorPositionArgsTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] the jump-to-cursor-position arguments templates array is empty");
        }
        s_Config.JumpToCursorPositionArgs = s_JumpToCursorPositionArgsTemplates[0].Args;
        s_Config.Save();
      }

      return true;
    }


    public static string GetNeovimVersion(string p)
    {
      // get Neovim installation version
      string version = "v-unknown";
      using var proc = ProcessUtils.HeadlessProcess();
      proc.StartInfo.FileName = p;
      proc.StartInfo.Arguments = "--version";
      proc.RunWithAssertion(s_Config.ProcessTimeout);
      var line = proc.StandardOutput.ReadLine();
      if (line != null)
      {
        version = line[(line.IndexOf(' ') + 1)..];
      }
      return version;
    }

    // because of the "InitializeOnLoad" attribute, this will be called when scripts in the project are recompiled
    static NeovimCodeEditor()
    {
      // config initialization
      s_Config = NeovimEditorConfig.Load();
      if (!SetDefaults())
        return;

      // initialize with project regeneration flags from config
      s_Generator = new ProjectGeneration(s_Config.CsprojFlags, s_Config.Analyzers);

      // if nvim executable path is already set in the config - check if it is still valid
      if (!string.IsNullOrWhiteSpace(s_Config.NvimExecutablePath))
      {
        string v;
        if (File.Exists(s_Config.NvimExecutablePath)
            && (v = GetNeovimVersion(s_Config.NvimExecutablePath)) != "v-unknown")
        {
          s_DiscoveredNeovimInstallations = new CodeEditor.Installation[] { new() {
            Name = $"Neovim {v}",
            Path = s_Config.NvimExecutablePath
          }};
        }
        else
        {
          Debug.LogWarning("[neovim.ide] the provided nvim executable path is no longer valid. Falling back to "
              + "automated nvim installation discovery (consider updating path via top menu: Neovim -> Settings ).");
          s_Config.NvimExecutablePath = null;
        }
      }

      // initialize the discovered Neovim installations array. The first 'path' is usually set to "nvim"
      // (or "nvim.exe"). That is obviously not a path but the expected name of Neovim on PATH (which is what the
      // CmdPath does here).
      if (!s_DiscoveredNeovimInstallations.Any())
      {
        s_DiscoveredNeovimInstallations = s_CandidateNeovimPaths
          .Select(p => p = Path.IsPathRooted(p) ? p : ProcessUtils.CmdPath(p, s_Config.ProcessTimeout))
          .Where(p => p != null && File.Exists(p))
          .Select(p =>
          {
            return new CodeEditor.Installation
            {
              Name = $"Neovim {GetNeovimVersion(p)}",
              Path = p,
            };
          })
          .ToArray();
      }

      // do NOT proceed if there aren't any discovered Neovim installations (i.e., not explicitly supplied in settings
      // and not installed in a common path).
      if (!s_DiscoveredNeovimInstallations.Any())
      {
        Debug.LogWarning("[neovim.ide] no Neovim installation was discovered. Consider explicitly providing an nvim "
            + "executable path via top menu: Neovim -> Settings");
        // TODO: show setting window
        return;
      }

      // we use the first discovered/set nvim installation path
      s_Config.NvimExecutablePath = s_DiscoveredNeovimInstallations.First().Path;

#if UNITY_EDITOR_LINUX
      s_LinuxPlatform = DetermineLinuxDesktopEnvironment();

      if (s_LinuxPlatform == LinuxDesktopEnvironment.X11)
      {
        if (ProcessUtils.CmdPath("wmctrl", s_Config.ProcessTimeout) == null)
        {
          Debug.LogWarning("[neovim.ide] neovim window focusing feature is not available \n"
              + "Reason: cmd 'wmctrl' is not available. Please install 'wmctrl' for window focusing capability.");
        }
        else
        {
          s_WindowFocusingAvailable = true;
        }
      }
      else if (s_LinuxPlatform == LinuxDesktopEnvironment.GNOME)
      {
        // this prompts the user to install a GNOME extension to focus on a window by title
        // there is unfortunately no other way to do this on GNOME under Wayland :/
        using var p = ProcessUtils.HeadlessProcess();
        p.StartInfo.FileName = "gnome-extensions";
        p.StartInfo.Arguments = "list";
        p.RunWithAssertion(10_000);
        const string uuid = "activate-window-by-title@lucaswerkmeister.de";
        var foundExtension = false;
        string line;
        while ((line = p.StandardOutput.ReadLine()) != null)
        {
          if (line.Contains(uuid))
          {
            foundExtension = true;
            s_WindowFocusingAvailable = true;
            break;
          }
        }

        // if the extension is not found, prompt the user to install it
        if (!foundExtension)
        {
          using var p2 = ProcessUtils.HeadlessProcess();
          p2.StartInfo.FileName = "busctl";
          p2.StartInfo.Arguments = $"--user call org.gnome.Shell.Extensions /org/gnome/Shell/Extensions org.gnome.Shell.Extensions InstallRemoteExtension s {uuid}";
          p2.Start();
          const string error = "[neovim.ide] neovim window focusing feature is not available\n"
              + "Reason: failed to install GNOME extension: activate-window-by-title@lucaswerkmeister.de\n";
          if (!p2.WaitForExit(15_000))
          {
            Debug.LogWarning($"{error}Reason: timed out after 10 seconds");
          }
          else if (p2.ExitCode != 0)
          {
            Debug.LogWarning($"{error}Reason: non-zero exit code ({p2.ExitCode})");
          }
          else
          {
            s_WindowFocusingAvailable = true;
          }
        }
      }
#elif UNITY_EDITOR_WIN
      s_WindowFocusingAvailable = true;
#endif

      EditorApplication.quitting += OnEditorQuitting;

      NeovimCodeEditor editor = new(s_Generator);
      CodeEditor.Register(editor);
    }


    public void CreateIfDoesntExist()
    {
      s_Generator.Sync();
    }


#if UNITY_EDITOR_LINUX
    private static LinuxDesktopEnvironment DetermineLinuxDesktopEnvironment()
    {
      string val = Environment.GetEnvironmentVariable("XDG_DATA_DIRS");
      if (val != null)
      {
        if (val.Contains("gnome", StringComparison.OrdinalIgnoreCase))
        {
          return LinuxDesktopEnvironment.GNOME;
        }
        else
        {
          return LinuxDesktopEnvironment.OTHER;
        }
      }
      return LinuxDesktopEnvironment.UNKNOWN;
    }
#endif

    public static bool TryChangeTermLaunchCmd(string cmd, string args, string env = null)
    {
      if (cmd.Contains("{app}"))  // in case the Neovim executable is invoked directly
      {
        cmd = cmd.Replace("{app}", CodeEditor.CurrentEditorPath);
        if (!File.Exists(CodeEditor.CurrentEditorPath))
          return false;
      }
      else  // or through terminal
      {
        if (Path.IsPathRooted(cmd))
        {
          if (!File.Exists(cmd))
            return false;
        }
        else if (ProcessUtils.CmdPath(cmd, s_Config.ProcessTimeout) == null)
          return false;
      }

      // serialize the new terminal launch command in Unity Editor's preferences settings
      s_Config.TermLaunchCmd = cmd;
      s_Config.TermLaunchArgs = args;
      s_Config.TermLaunchEnv = env;

#if UNITY_EDITOR_WIN
      s_Config.PrevServerSocket = null;
#endif

      s_Config.Save();
      return true;
    }

    private static readonly CodeEditor.Installation[] s_DiscoveredNeovimInstallations = new CodeEditor.Installation[0];
    public CodeEditor.Installation[] Installations => s_DiscoveredNeovimInstallations;


    public NeovimCodeEditor(IGenerator projectGeneration)
    {
      s_Generator = projectGeneration;
    }


    // Callback to the IExternalCodeEditor when it has been chosen from the PreferenceWindow.
    public void Initialize(string editorInstallationPath) { }


    // Unity stores the path of the chosen editor. An instance of
    // IExternalCodeEditor can take responsibility for this path, by returning
    // true when this method is being called. The out variable installation need
    // to be constructed with the path and the name that should be shown in the
    // "External Tools" code editor list.
    public bool TryGetInstallationForPath(string editorPath, out CodeEditor.Installation installation)
    {
      editorPath = Path.GetFullPath(editorPath);
      installation = s_DiscoveredNeovimInstallations.FirstOrDefault(i => i.Path == editorPath);
      return !Equals(installation, default(CodeEditor.Installation));
    }

    /// <summary>
    /// Tries to add the provided analyzer. If successful, the underlying config is serialized and the project
    /// generator is syncronized.
    /// </summary>
    public static bool TryAddAnalyzer(string path)
    {
      if (s_Config.TryAddAnalyzer(path))
      {
        // Debug.Log($"[neovim.ide] added analyzer: {Path.GetFileName(path)}");
        s_Config.Save();
        CodeEditor.Editor.CurrentCodeEditor.SyncAll();
        return true;
      }
      return false;
    }

    /// <summary>
    /// Deletes analyzer at provided index, serializes the underlying config, and syncs the generator.
    /// </summary>
    public static void DelAnalyzerAt(int i)
    {
      s_Config.DelAnalyzerAt(i);
      s_Config.Save();
      s_Generator.Sync();
    }

    /// <summary>
    /// Reset the Neovim configuration by deleting the saved EditorPrefs and re-initializing.
    /// Use this when settings become corrupted or you want to start fresh.
    /// </summary>
    public static void ResetConfig()
    {
      NeovimEditorConfig.Reset();
      s_Config = NeovimEditorConfig.Load();

      // set some defaults in case they are not already set (launch cmd and args, open-file args, etc.)
      if (!SetDefaults())
        return;

      // sync deserialized analyzers with the project generator's analyzers
      s_Generator.SetAnalyzers(s_Config.Analyzers);
      s_Generator.AssemblyNameProvider.CsprojFlags = s_Config.CsprojFlags;
      s_Generator.Sync();
    }

    private static void OnEditorQuitting()
    {
      if (!s_Config.KillNvimOnQuit)
        return;
      KillNvimServer();
    }

    /// <summary>
    /// Gracefully shuts down the nvim server for this Unity instance by sending ":qa!" via --remote-send.
    /// This allows nvim to clean up swap files and session state properly (unlike kill -9).
    /// </summary>
    public static void KillNvimServer()
    {
      if (!IsNvimServerInstanceAlreadyRunning())
        return;
      try
      {
        using var p = ProcessUtils.HeadlessProcess();
        p.StartInfo.FileName = s_Config.NvimExecutablePath ?? "nvim";
        p.StartInfo.Arguments = $"--server {s_ServerSocket} --remote-send \":qa!<CR>\"";
#if UNITY_EDITOR_WIN
        p.StartInfo.Arguments = $"--server {s_Config.PrevServerSocket} --remote-send \":qa!<CR>\"";
#endif
        p.RunWithAssertion(s_Config.ProcessTimeout);
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[neovim.ide] failed to kill nvim server: {e.Message}");
      }
    }

    /// <summary>
    /// Force-kills ALL orphaned nvim processes that were listening on /tmp/nvimsocket_* sockets.
    /// Use this as a last resort when nvim servers were left behind by crashed Unity instances
    /// and can no longer be reached via --remote-send. This is a destructive operation that kills
    /// processes with SIGTERM (pkill) or /F (taskkill) — unsaved buffers will be lost.
    /// </summary>
    public static void KillOrphanedNvimServers()
    {
#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      try
      {
        using var p = ProcessUtils.HeadlessProcess();
        p.StartInfo.FileName = "pkill";
        p.StartInfo.Arguments = "-f \"nvim.*--listen /tmp/nvimsocket_\"";
        p.Start();
        p.WaitForExit(3000);
        Debug.Log("[neovim.ide] killed orphaned nvim server processes.");
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[neovim.ide] failed to kill orphaned nvim servers: {e.Message}");
      }
#elif UNITY_EDITOR_WIN
      try
      {
        using var p = ProcessUtils.HeadlessProcess();
        p.StartInfo.FileName = "taskkill";
        p.StartInfo.Arguments = "/F /IM nvim.exe";
        p.Start();
        p.WaitForExit(3000);
        Debug.Log("[neovim.ide] killed orphaned nvim server processes.");
      }
      catch (Exception e)
      {
        Debug.LogWarning($"[neovim.ide] failed to kill orphaned nvim servers: {e.Message}");
      }
#endif
    }

    // Unity calls this method when it populates "Preferences/External Tools"
    // in order to allow the code editor to generate necessary GUI. For example,
    // when creating an an argument field for modifying the arguments sent to
    // the code editor.
    public void OnGUI()
    {
      // internal bug in some Unity versions cause the call NeovimSettingsWindow.ShowWindow() to generate:
      // "EndLayoutGroup: BeginLayoutGroup must be called first" errors ...
      EditorGUILayout.HelpBox(
        "Configure all Neovim-specific settings by going to the top menu: Neovim => Settings",
        MessageType.Info
      );
    }


    public static ProjectGenerationFlag CsprojFlags
    {
      get => s_Config.CsprojFlags;
      set
      {
        if (value == s_Config.CsprojFlags)
          return;
        s_Config.CsprojFlags = s_Generator.AssemblyNameProvider.CsprojFlags = value;
        s_Generator.Sync();
      }
    }


    // When you change Assets in Unity, this method for the current chosen
    // instance of IExternalCodeEditor parses the new and changed Assets.
    public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles,
        string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
    {
      s_Generator.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToList(),
          importedFiles);
    }


    // Unity calls this function during initialization in order to sync the
    // Project. This is different from SyncIfNeeded in that it does not get a
    // list of changes.
    public void SyncAll()
    {
      AssetDatabase.Refresh();
      s_Generator.Sync();
    }


    /// <summary>
    /// Checks if an nvim server instance is currently running (for this or previous Unity session) by checking
    /// whether the current server socket is live.
    /// </summary>
    public static bool IsNvimServerInstanceAlreadyRunning()
    {
#if UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      // Connect to the domain socket rather than checking file existence — a stale socket file is
      // left behind when Neovim crashes, which would otherwise cause a false positive.
      // IsUnixSocketAlive also deletes the file if the socket is stale.
      return NetUtils.IsUnixSocketAlive(s_ServerSocket);
#else  // UNITY_EDITOR_WIN
      // this is tricky... using PIDs did not work... domain sockets have an issue on the side of NeoVim...
      // since on Windows we use a randomly available port for the TCP NeoVim server socket, we can know
      // whether a NeoVim server instance is running by trying to bind a TCP listener to the previously used
      // port
      string prevAddr = s_Config.PrevServerSocket;
      if (string.IsNullOrWhiteSpace(prevAddr)) return false;

      int idx = prevAddr.IndexOf(':');
      string ip = prevAddr[..idx];
      int port = int.Parse(prevAddr[(idx + 1)..]);
      return NetUtils.IsPortInUse(ip, port);
#endif
    }


    // The external code editor needs to handle the request to open a file.
    // Note that by returning 'false' Unity will try to open the file in a different program which is
    // the reason why, for instance, we return 'false' for image files
    public bool OpenProject(string filePath = "", int line = -1, int column = -1)
    {
      if (!string.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath)) return false;
      if (line == -1) line = 1;
      if (column == -1) column = 0;

      // we want to return false in case a different editor is supplied (e.g., code.cmd for VSCode)
      if (!Array.Exists(_supportedFileNames, fn =>
            string.Compare(fn, Path.GetFileName(CodeEditor.CurrentEditorPath), StringComparison.OrdinalIgnoreCase) == 0))
        return false;

      // only use NeoVim for reasonable file extensions (e.g., do not use NeoVim to open .png files which happens
      // without this check). Skip extension check when filePath is empty (e.g., "Assets/Open C# project").
      if (!string.IsNullOrWhiteSpace(filePath) && !Array.Exists(s_SupportedExtensions, e => e.ToLower() == Path.GetExtension(filePath)
            .TrimStart('.')
            .ToLower()))
        return false;

#if UNITY_EDITOR_WIN
      string app = $"\"{CodeEditor.CurrentEditorPath}\"";
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
      string app = CodeEditor.CurrentEditorPath;
#endif

      // get terminal launch cmd and its args from Unity editor preferences
      string termLaunchCmd = s_Config.TermLaunchCmd;
      string termLaunchArgs = s_Config.TermLaunchArgs;
      string termLaunchEnv = s_Config.TermLaunchEnv;

      if (!IsNvimServerInstanceAlreadyRunning())
      {
        try
        {
          using Process p = new();
          p.StartInfo.FileName = termLaunchCmd
            .Replace("{app}", app);
          p.StartInfo.Arguments = termLaunchArgs
            .Replace("{app}", app)
            .Replace("{filePath}", string.IsNullOrWhiteSpace(filePath) ? "" : $"\"{filePath}\"")
            .Replace("{serverSocket}", s_ServerSocket)
            .Replace("{instanceId}", s_InstanceId)
#if UNITY_EDITOR_WIN
            .Replace("{getProcessPPIDScriptPath}", s_GetProcessPPIDPath)
#endif
          ;

          // pass optionally-set environment variables to process
          if (!string.IsNullOrWhiteSpace(termLaunchEnv))
          {
            foreach (var env in termLaunchEnv.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
              var envKey = env.Split('=', 2);
              if (envKey.Length == 2)
              {
                p.StartInfo.Environment[envKey[0]] = envKey[1];
              }
              else
              {
                Debug.LogWarning($"[neovim.ide] failed to parse environment variable entry from: {env}. Expected format is: ENV=VALUE");
              }
            }
          }

          p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
          p.StartInfo.CreateNoWindow = false;
          p.StartInfo.UseShellExecute = false;
          // Debug.Log($"{p.StartInfo.FileName} {p.StartInfo.Arguments}");

          // start and do not care (do not wait for exit)
          p.Start();

#if UNITY_EDITOR_WIN

          // save the server socket so that we can communicate with it later
          // (e.g., when Unity exits but the server is still running)
          s_Config.PrevServerSocket = s_ServerSocket;
          s_Config.Save();

          // the idea here is to figure out the handle of the process running the Neovim server instance
          // this is a bit tricky on Windows - because depending on the terminal launch cmd, it might
          // spawn a child process or it might not.
          //
          // first - we assume that the terminal launch cmd's process is the one that has Neovim server
          // open (i.e., no child process)
          int process_startup_timeout = 1000;
          try
          {
            IntPtr wh = ProcessUtils.GetWindowHandle(p, process_startup_timeout);
            s_Config.PrevServerProcessIntPtrStringRepr = wh.ToString();
            s_Config.Save();
          }
          // this probably means that the terminal launch cmd spawns a new child instance that is responsible for the Neovim window
          catch (InvalidOperationException)
          {
            // pipe's name should be the same as in "GetProcessPPID.ps1" script
            using var pipeClient = new NamedPipeClientStream(".", @"\\.\pipe\getprocessppidpipe", PipeDirection.In);
            pipeClient.Connect(1000);
            using var _sr = new StreamReader(pipeClient);
            string ppidStr = _sr.ReadLine() ?? throw new Exception("PPID received string is null");
            var ppid = int.Parse(ppidStr);

            Process neovimServerProcess = Process.GetProcessById(ppid);
            IntPtr wh = ProcessUtils.GetWindowHandle(neovimServerProcess, process_startup_timeout);
            s_Config.PrevServerProcessIntPtrStringRepr = wh.ToString();
            s_Config.Save();
          }
          catch (Exception)
          {
            s_WindowFocusingAvailable = false;
            Debug.LogWarning($"[neovim.ide] failed to get the PID of the window responsible for the Neovim server instance."
                + " Auto window focusing is disabled");
          }
#endif
        }
        catch (Exception e)
        {
          Debug.LogError($"[neovim.ide] failed to create a Neovim server instance. Reason: {e}");
          return false;
        }
      }

#if UNITY_EDITOR_WIN
      // on Windows, listening to a domain socket yields the following error: "neovim Failed to --listen: service not available for socket type"
      // so we have to listen to a TCP socket instead with a local addr and a random port - this will be overwitten below
      s_ServerSocket = s_Config.PrevServerSocket;
#endif

      // send request to Neovim server instance listening on the provided socket path to open a tab/buffer corresponding
      // to the provided filepath. Skip when filePath is empty (e.g., "Assets/Open C# project").
      if (!string.IsNullOrWhiteSpace(filePath))
      {
        int currentMods = Event.current != null ? (int)Event.current.modifiers : 0;
        const int relevantMask = (int)(EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt);
        currentMods &= relevantMask;

        var binding = s_Config.ModifierBindings
          .FirstOrDefault(b => (b.Modifiers & relevantMask) == currentMods)
          ?? s_Config.ModifierBindings.FirstOrDefault(b => b.Modifiers == 0);

        string openFileArgs = binding?.Args ?? s_OpenFileArgsTemplates[0].Args;

        string args = openFileArgs
          .Replace("{serverSocket}", s_ServerSocket)
          .Replace("{filePath}", $"\"{filePath}\"");

        using var p = ProcessUtils.HeadlessProcess();
        p.StartInfo.FileName = app;
        p.StartInfo.Arguments = args;
#if UNITY_EDITOR_WIN
        // on Windows, for some reason the process executes correctly but without exiting within any given timeout
        // to fix that, we simply catch the TimeoutException and kill the process.
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (TimeoutException) { }
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
        // life is ez on Linux (unless you deal with any window manager...)
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (ExitCodeMismatchException e)
        {
          Debug.LogWarning($"[neovim.ide] failed to open file in Neovim server. Exit code: {e.Actual}. Is the server running?");
        }
        catch (TimeoutException) { }
#endif
      }

      /*
      * now send request to jump cursor to exact position. You cannot do both --remote-tab and --remote-send at the
      * same time (this is a limitation of the Neovim CLI as it will only execute the last --remote argument and not
      * both)
      */
      if (line != 1 || column != 0)
      {
        string args = s_Config.JumpToCursorPositionArgs
          .Replace("{serverSocket}", s_ServerSocket)
          .Replace("{line}", line.ToString())
          .Replace("{column}", column.ToString());

        using var p = ProcessUtils.HeadlessProcess();
        p.StartInfo.FileName = app;
        p.StartInfo.Arguments = args;
#if UNITY_EDITOR_WIN
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (TimeoutException) { }
#else  // UNITY_EDITOR_LINUX || UNITY_EDITOR_OSX
        try
        {
          p.RunWithAssertion(s_Config.ProcessTimeout);
        }
        catch (ExitCodeMismatchException) { }
        catch (TimeoutException) { }
#endif
      }

      // optionally focus on Neovim - this is extremely tricky to implement across platforms
      if (!s_WindowFocusingAvailable)
        return true;

#if UNITY_EDITOR_LINUX
      switch (s_LinuxPlatform)
      {
        case LinuxDesktopEnvironment.X11:
          {
            using var p = ProcessUtils.HeadlessProcess();
            p.StartInfo.FileName = "wmctrl";
            p.StartInfo.Arguments = $"-a nvimunity-{s_InstanceId}";
            var error_msg = $"[neovim.ide] failed to focus on Neovim server instance titled 'nvimunity-{s_InstanceId}'.\n"
              + $"Reason: cmd `{p.StartInfo.FileName}` with args `{p.StartInfo.Arguments}` failed.\n";
            try
            {
              p.RunWithAssertion(s_Config.ProcessTimeout);
            }
            catch (ExitCodeMismatchException)
            {
              Debug.LogWarning($"{error_msg}Reason: non-zero exit code.");
            }
            catch (TimeoutException)
            {
              Debug.LogWarning($"{error_msg}Exception message: timed out after {s_Config.ProcessTimeout} milliseconds.");
            }
            break;
          }
        case LinuxDesktopEnvironment.GNOME:
          {
            // a clusterfuck of a mess - TODO: learn gdbus and clean this shit up somehow
            using var p = ProcessUtils.HeadlessProcess();
            p.StartInfo.FileName = "gdbus";
            p.StartInfo.Arguments = $@"call --session --dest org.gnome.Shell \
--object-path /de/lucaswerkmeister/ActivateWindowByTitle \
--method de.lucaswerkmeister.ActivateWindowByTitle.activateBySubstring 'nvimunity-{s_InstanceId}'";
            string error_msg = $"[neovim.ide] failed to focus on Neovim server instance titled 'nvimunity-{s_InstanceId}'.\n"
                  + "Did you logout and login of your GNOME session?\n"
                  + "Did you install the 'activate-window-by-title@lucaswerkmeister.de' GNOME extension?\n";
            try
            {
              p.RunWithAssertion(s_Config.ProcessTimeout);
            }
            catch (ExitCodeMismatchException)
            {
              Debug.LogWarning($"{error_msg}Reason: non-zero exit code.");
            }
            catch (TimeoutException)
            {
              Debug.LogWarning($"{error_msg}Exception message: timed out after {s_Config.ProcessTimeout} milliseconds.");
            }
            break;
          }
        case LinuxDesktopEnvironment.KDE:
          {
            // TODO: add support for switching focus to Neovim on KDE Wayland
          }
          break;
        default:
          // do nothing - too complicated to make it work on all desktop environments :/
          break;
      }
#elif UNITY_EDITOR_WIN
      IntPtr windowHandle = new(Convert.ToInt64(s_Config.PrevServerProcessIntPtrStringRepr));
      ShowWindow(windowHandle, 5);  // 5 == Activates the window and displays it in its current size and position
      SetForegroundWindow(windowHandle);
#endif

      return true;
    }
  }
}
