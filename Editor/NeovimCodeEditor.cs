#pragma warning disable IDE0130
using System;
using System.Collections.Generic;
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

#if UNITY_EDITOR_LINUX
    static string s_ServerSocket = "/tmp/nvimsocket";
#else // UNITY_EDITOR_WIN
    // this will be initialized to some "127.0.0.1:<random-port>" because Unix domain sockets on Windows are a bitch
    static string s_ServerSocket;
    static readonly string s_GetProcessPPIDPath = Path.GetFullPath("Packages/com.walcht.ide.neovim/GetProcessPPID.ps1");
#endif

    /// <summary>
    ///   These are the default template arguments that one of which can potentially be used
    ///   to send request to the Neovim server instance upon opening a file (or clicking on
    ///   error message in console, etc).
    ///   First entry is the default.
    /// </summary>
    public static readonly string[] s_OpenFileArgsTemplates = {
      "--server {serverSocket} --remote-tab {filePath}",
    };

    /// <summary>
    ///   These are the default template arguments that one of which can potentially be used
    ///   to send request to the Neovim server instance to jump to a given cursor position.
    ///   First entry is the default.
    /// </summary>
    public static readonly string[] s_JumpToCursorPositionArgsTemplates = {
      "--server {serverSocket} --remote-send \":call cursor({line},{column})<CR>\"",
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
#else  // UNITY_EDITOR_WIN
    [DllImport("user32.dll")]
    internal static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
#endif

    // terminal launch command template - use this template for adding new launch cmds
    public static readonly (string, string, string) s_TermLaunchCmdTemplate = ("<terminal-emulator>", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocket}", "{environment}");

    // list of neovim launch cmds from popular terminal emulators - this is
    // just a hardcoded list so that non-tech-savy users can just get to
    // using Neovim with minimal friction.
    public static readonly (string, string, string)[] s_TermLaunchCmds =
#if UNITY_EDITOR_LINUX
    {
      ("gnome-terminal", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocket}", "{environment}"),
      ("alacritty", "--title \"nvimunity\" --command {app} {filePath} --listen {serverSocket}", "{environment}"),
      ("ptyxis", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocket}", "{environment}"),
      ("xterm", "-T \"nvimunity\" -e {app} {filePath} --listen {serverSocket}", "{environment}"),
      ("ghostty", "--title=\"nvimunity\" --command='{app} {filePath} --listen {serverSocket}'", "{environment}"),
    };
#else  // UNITY_EDITOR_WIN
    {
      // on Powershell, replace the ';' with "`;"
      ("wt", "nt {app} {filePath} --listen {serverSocket} ; nt Powershell -File {getProcessPPIDScriptPath}", ""),
      ("alacritty", "--title \"nvimunity\" --command {app} {filePath} --listen {serverSocket}", ""),
    };
#endif

    // Neovim installation paths on Linux here - the first valid path is picked otherwise the ENV variable TODO is
    // checked
    private static readonly string[] s_CandidateNeovimPaths =
#if UNITY_EDITOR_LINUX
     {
       "nvim",
       "/usr/bin/nvim",
       "/opt/nvim-linux64/bin/nvim",
       "/opt/nvim-linux-x86_64/bin/nvim",
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
    /// and jump-to-cursor-position request arguments in case any of them is null.
    /// </summary>
    private static bool SetDefaults()
    {
      // get terminal launch cmd and its args from Unity editor preferences
      string termLaunchCmd = s_Config.TermLaunchCmd;
      string termLaunchArgs = s_Config.TermLaunchArgs;
      string termLaunchEnv = s_Config.TermLaunchEnv;

      // if cmd is empty/whitespace => no terminal launch cmd has been provided/chosen yet
      if (string.IsNullOrWhiteSpace(termLaunchCmd) || string.IsNullOrWhiteSpace(termLaunchArgs))
      {
        // pick the first default available terminal from the list of 'popular' terminal emulators. Obviously this is
        // some sort of a heuristic but the user can explicitly change this through the GUI.
        bool s = false;
        foreach (var termLaunch in s_TermLaunchCmds)
        {
          if (TryChangeTermLaunchCmd(termLaunch))
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
              "You have to set the terminal launch command by going to the menu item: Neovim => ChangeTerminalLaunchCmd");
          return false;
        }
      }

      if (string.IsNullOrWhiteSpace(s_Config.OpenFileArgs))
      {
        if (!s_OpenFileArgsTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] open-file template list is empty");
        }
        s_Config.OpenFileArgs = s_OpenFileArgsTemplates[0];
        s_Config.Save();
      }

      if (string.IsNullOrWhiteSpace(s_Config.JumpToCursorPositionArgs))
      {
        if (!s_JumpToCursorPositionArgsTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] the jump-to-cursor-position arguments templates array is empty");
        }
        s_Config.JumpToCursorPositionArgs = s_JumpToCursorPositionArgsTemplates[0];
        s_Config.Save();
      }

      return true;
    }

    public static void InitConfig()
    {
      s_Config = NeovimEditorConfig.Load();

      // set some defaults in case they are not already set (launch cmd and args, open-file args, etc.)
      if (!SetDefaults())
        return;

      // sync deserialized analyzers with the project generator's analyzers
      s_Generator.SetAnalyzers(s_Config.Analyzers);
    }

    // because of the "InitializeOnLoad" attribute, this will be called when scripts in the project are recompiled
    static NeovimCodeEditor()
    {

      s_Generator = new SdkStyleProjectGeneration();

      InitConfig();

      // initialize the discovered Neovim installations array
      s_DiscoveredNeovimInstallations = s_CandidateNeovimPaths
        .Where(p => File.Exists(p) || ProcessUtils.CheckCmdExistence($"\"{p}\""))
        .Select((p, _) =>
        {
          // get Neovim installation version
          string version = "v-unknown";
          ProcessUtils.GetCmdStdOutput($"\"{p}\" --version", out List<string> output_lines);
          if (output_lines.Any() && !string.IsNullOrWhiteSpace(output_lines[0]))
          {
            version = output_lines[0][(output_lines[0].IndexOf(' ') + 1)..];
          }
          return new CodeEditor.Installation
          {
            Name = $"Neovim {version}",
            Path = Path.GetFullPath(p),
          };
        })
        .ToArray();

      // do NOT proceed if there aren't any discovered Neovim installations
      if (!s_DiscoveredNeovimInstallations.Any())
      {
        Debug.LogWarning("[neovim.ide] no Neovim installation was discovered");
        return;
      }

#if UNITY_EDITOR_LINUX
      s_LinuxPlatform = DetermineLinuxDesktopEnvironment();

      if (s_LinuxPlatform == LinuxDesktopEnvironment.X11)
      {
        if (!ProcessUtils.CheckCmdExistence("wmctrl"))
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
        if (!ProcessUtils.RunShellCmd(@"
UUID=activate-window-by-title@lucaswerkmeister.de
if ! gnome-extensions list | grep --quiet $UUID; then
  busctl --user call org.gnome.Shell.Extensions /org/gnome/Shell/Extensions org.gnome.Shell.Extensions InstallRemoteExtension s $UUID
fi
", timeout: 10000))
        {
          Debug.LogWarning("[neovim.ide] neovim window focusing feature is not available \n"
              + "Reason: failed to install GNOME extension: activate-window-by-title@lucaswerkmeister.de");
        }
        else
        {
          s_WindowFocusingAvailable = true;
        }
      }
#else  // UNITY_EDITOR_WIN
      s_WindowFocusingAvailable = true;
#endif

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

    public static bool TryChangeTermLaunchCmd((string, string, string) termLaunch)
    {
      (string cmd, string args, string env) = termLaunch;

      if (cmd.Contains("{app}"))  // in case the Neovim executable is invoked directly
      {
        cmd = cmd.Replace("{app}", CodeEditor.CurrentEditorPath);
        if (!File.Exists(CodeEditor.CurrentEditorPath))
          return false;
      }
      else  // or through terminal
      {
        if (!ProcessUtils.CheckCmdExistence(cmd))
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

    private static readonly CodeEditor.Installation[] s_DiscoveredNeovimInstallations;
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

    private static void TryAddAnalyzer(string path)
    {
      if (s_Config.TryAddAnalyzer(path))
      {
        Debug.Log($"[neovim.ide] added analyzer: {Path.GetFileName(path)}");
        s_Config.Save();
        s_Generator.Sync();
      }
    }

    private const int EDITOR_GUI_ELEMENT_HEIGHT = 37;

    private Vector2 m_ScrollViewPos;
    // Unity calls this method when it populates "Preferences/External Tools"
    // in order to allow the code editor to generate necessary GUI. For example,
    // when creating an an argument field for modifying the arguments sent to
    // the code editor.
    public void OnGUI()
    {
      EditorGUILayout.BeginHorizontal();
      {
        TryAddAnalyzer(EditorGUILayout.DelayedTextField("Add analyzer: ", string.Empty));
        if (GUILayout.Button("Browse", GUILayout.Width(100)))
          TryAddAnalyzer(EditorUtility.OpenFilePanel("Select analyzer to add (.dll)", "", "dll"));
      }
      EditorGUILayout.EndHorizontal();

      // show currently used custom analyzers
      if (s_Config.Analyzers.Any())
      {
        EditorGUILayout.LabelField("Current analyzers: ");
        EditorGUI.indentLevel++;
        m_ScrollViewPos = EditorGUILayout.BeginScrollView(m_ScrollViewPos, GUILayout.Height(Math.Min(s_Config.Analyzers.Count * EDITOR_GUI_ELEMENT_HEIGHT, 3 * EDITOR_GUI_ELEMENT_HEIGHT)));
        {
          for (int i = s_Config.Analyzers.Count - 1; i >= 0; --i)
          {
            EditorGUILayout.BeginHorizontal();
            {
              EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(s_Config.Analyzers[i]) + ": ", GUILayout.Width(233));
              EditorGUILayout.LabelField(s_Config.Analyzers[i], GUILayout.ExpandWidth(true));
              if (GUILayout.Button("Remove", GUILayout.Width(100)))
              {
                s_Config.DelAnalyzerAt(i);
                s_Config.Save();
                s_Generator.Sync();
              }
            }
            EditorGUILayout.EndHorizontal();
          }
        }
        EditorGUILayout.EndScrollView();
        EditorGUI.indentLevel--;
      }

      EditorGUILayout.LabelField("Generate .csproj files for:");
      EditorGUI.indentLevel++;
      {
        SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages", "");
        SettingsButton(ProjectGenerationFlag.Local, "Local packages", "");
        SettingsButton(ProjectGenerationFlag.Registry, "Registry packages", "");
        SettingsButton(ProjectGenerationFlag.Git, "Git packages", "");
        SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages", "");
        SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball", "");
        SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources", "");
        SettingsButton(ProjectGenerationFlag.PlayerAssemblies, "Player projects", "For each player project generate an additional csproj with the name 'project-player.csproj'");
        RegenerateProjectFiles();
      }
      EditorGUI.indentLevel--;
    }


    private void RegenerateProjectFiles()
    {
      var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
      rect.width = 252;
      if (GUI.Button(rect, "Regenerate project files"))
      {
        s_Generator.Sync();
      }
    }


    private void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip)
    {
      var prevValue = s_Generator.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
      var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
      if (newValue != prevValue)
      {
        s_Generator.AssemblyNameProvider.ToggleProjectGeneration(preference);
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


    public bool IsNvimServerInstanceAlreadyRunning()
    {
#if UNITY_EDITOR_LINUX
      // On Linux, since we use domain sockets, we can rely on the existence of the socket to know whether there
      // is an already running nvim server instance
      return File.Exists(s_ServerSocket);
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
      // without this check)
      if (!Array.Exists(s_SupportedExtensions, e => e.ToLower() == Path.GetExtension(filePath)
            .TrimStart('.')
            .ToLower()))
        return false;

#if UNITY_EDITOR_LINUX
      string app = CodeEditor.CurrentEditorPath;
#else // UNITY_EDITOR_WIN
      string app = $"\"{CodeEditor.CurrentEditorPath}\"";
#endif

      // get terminal launch cmd and its args from Unity editor preferences
      string termLaunchCmd = s_Config.TermLaunchCmd;
      string termLaunchArgs = s_Config.TermLaunchArgs;
      string termLaunchEnv = s_Config.TermLaunchEnv;

      if (!IsNvimServerInstanceAlreadyRunning())
      {
#if UNITY_EDITOR_WIN
        // on Windows, listening to a domain socket yields the following error: "neovim Failed to --listen: service not
        // available for socket type" so we have to listen to a TCP socket instead with a local addr and a random port
        s_ServerSocket = $"127.0.0.1:{NetUtils.GetRandomAvailablePort()}";
#endif

        try
        {
          using Process p = new();
          p.StartInfo.FileName = termLaunchCmd
            .Replace("{app}", app);
          p.StartInfo.Arguments = termLaunchArgs
            .Replace("{app}", app)
            .Replace("{filePath}", $"\"{filePath}\"")
            .Replace("{serverSocket}", s_ServerSocket)
#if UNITY_EDITOR_WIN
            .Replace("{getProcessPPIDScriptPath}", s_GetProcessPPIDPath)
#endif
          ;

#if UNITY_EDITOR_LINUX
          if (!string.IsNullOrWhiteSpace(termLaunchEnv) && !termLaunchEnv.Contains("{environment}"))
          {

            foreach (var env in termLaunchEnv.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
              var envKey = env.Split('=', 2);
              if (envKey.Length == 2)
                p.StartInfo.Environment[envKey[0]] = envKey[1];
            }

          }
#endif

          p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
          p.StartInfo.CreateNoWindow = false;
          p.StartInfo.UseShellExecute = false;  // allow Environment to be pass on linux
#if UNITY_EDITOR_WIN
          p.StartInfo.UseShellExecute = true;  // has to be true on Windows
#endif
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
      // to the provided filepath
      {
        string args = s_Config.OpenFileArgs
          .Replace("{serverSocket}", s_ServerSocket)
          .Replace("{filePath}", $"\"{filePath}\"");

        ProcessUtils.RunShellCmd($"{app} {args}", timeout: s_Config.ProcessTimeout);
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

        ProcessUtils.RunShellCmd($"{app} {args}", timeout: s_Config.ProcessTimeout);
      }

      // optionally focus on Neovim - this is extremely tricky to implement across platforms
      if (!s_WindowFocusingAvailable)
        return true;

#if UNITY_EDITOR_LINUX
      switch (s_LinuxPlatform)
      {
        case LinuxDesktopEnvironment.X11:
          {
            string cmd = @"wmctrl -a nvimunity";
            if (!ProcessUtils.RunShellCmd(cmd, timeout: s_Config.ProcessTimeout))
            {
              Debug.LogWarning($"[neovim.ide] failed to focus on Neovim server instance titled 'nvimunity'.\n"
                  + $"Failed to execute the cmd: '{cmd}'");
            }
            break;
          }
        case LinuxDesktopEnvironment.GNOME:
          {
            // a clusterfuck of a mess - TODO: learn gdbus and clean this shit up somehow
            string cmd = @"gdbus call --session --dest org.gnome.Shell \
--object-path /de/lucaswerkmeister/ActivateWindowByTitle \
--method de.lucaswerkmeister.ActivateWindowByTitle.activateBySubstring 'nvimunity'";
            if (!ProcessUtils.RunShellCmd(cmd, timeout: s_Config.ProcessTimeout))
            {
              Debug.LogWarning($"[neovim.ide] failed to focus on Neovim server instance titled 'nvimunity'.\n"
                  + "Did you logout and login of your GNOME session?\n"
                  + "Did you install the 'activate-window-by-title@lucaswerkmeister.de' GNOME extension?");
            }
          }
          break;
        case LinuxDesktopEnvironment.KDE:
          {
            // TODO: add support for switching focus to Neovim on KDE Wayland
          }
          break;
        default:
          // do nothing - too complicated to make it work on all desktop environments :/
          break;
      }
#else  // UNITY_EDITOR_WIN
      IntPtr windowHandle = new(Convert.ToInt64(s_Config.PrevServerProcessIntPtrStringRepr));
      ShowWindow(windowHandle, 5);  // 5 == Activates the window and displays it in its current size and position
      SetForegroundWindow(windowHandle);
#endif

      return true;
    }
  }
}
