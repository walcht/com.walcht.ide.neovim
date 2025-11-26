#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX // no support for MacOS yet...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
using Debug = UnityEngine.Debug;
using System.Threading.Tasks;


namespace Neovim.Editor
{
  [InitializeOnLoad]
  public class NeovimCodeEditor : IExternalCodeEditor
  {
    static readonly string[] _supportedFileNames = { "nvim", "nvim.exe" };
    static readonly bool s_WindowFocusingAvailable = false;
    static string s_ServerSocket = "/tmp/nvimsocket";

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
#endif

    // terminal launch command template - use this template for adding new launch cmds
    public static readonly (string, string) s_TermLaunchCmdTemplate = ("<terminal-emulator>", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocket}");

    // list of neovim launch cmds from popular terminal emulators - this is
    // just a hardcoded list so that non-tech-savy users can just get to
    // using Neovim with minimal friction.
    public static readonly (string, string)[] s_TermLaunchCmds =
#if UNITY_EDITOR_LINUX
    {
        ("gnome-terminal", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocket}"),
        ("alacritty", "--title \"nvimunity\" --command {app} {filePath} --listen {serverSocket}"),
        ("ptyxis", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocket}"),
        ("xterm", "-T \"nvimunity\" -e {app} {filePath} --listen {serverSocket}"),
    };
#else
    {
        ("{app}", "{filePath} --listen {serverSocket}"),  // run nvim.exe directly
        ("alacritty", "--title \"nvimunity\" --command {app} {filePath} --listen {serverSocket}"),
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

    private IGenerator m_Generator = null;

    private static bool SetDefaults()
    {
      // get terminal launch cmd and its args from Unity editor preferences
      string termLaunchCmd = EditorPrefs.GetString("NvimUnityTermLaunchCmd");
      string termLaunchArgs = EditorPrefs.GetString("NvimUnityTermLaunchArgs");

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

      if (string.IsNullOrWhiteSpace(EditorPrefs.GetString("NvimUnityOpenFileArgs"))) {
        if (!s_OpenFileArgsTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] TODO");
          return false;
        }
        EditorPrefs.SetString("NvimUnityOpenFileArgs", s_OpenFileArgsTemplates[0]);
      }

      if (string.IsNullOrWhiteSpace(EditorPrefs.GetString("NvimUnityJumpToCursorPositionArgs")))
      {
        if (!s_JumpToCursorPositionArgsTemplates.Any())
        {
          Debug.LogError($"[neovim.ide] TODO");
          return false;
        }
        EditorPrefs.SetString("NvimUnityJumpToCursorPositionArgs", s_JumpToCursorPositionArgsTemplates[0]);
      }

      return true;
    }

    // because of the "InitializeOnLoad" attribute, this will be called when scripts in the project are recompiled
    static NeovimCodeEditor()
    {
      // set some defaults in case they are not already set (launch cmd and args, open-file args, etc.)
      if (!SetDefaults())
        return;

      // initialize the discovered Neovim installations array
      s_DiscoveredNeovimInstallations = s_CandidateNeovimPaths
        .Where(p => File.Exists(p) || ProcessUtils.CheckCmdExistence($"\"{p}\""))
        .Select((p, _) => {
          // get Neovim installation version
          string version = "v-unknown";
          List<string> output_lines;
          ProcessUtils.GetCmdStdOutput($"\"{p}\" --version", out output_lines);
          if (output_lines.Any() && !String.IsNullOrWhiteSpace(output_lines[0]))
          {
            version = output_lines[0].Substring(output_lines[0].IndexOf(' ') + 1);
          }
          return new CodeEditor.Installation{
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
      // TODO: add auto Window focus on Windows platforms
      s_WindowFocusingAvailable = false;
#endif

      NeovimCodeEditor editor = new(GeneratorFactory.GetInstance(GeneratorStyle.SDK));
      CodeEditor.Register(editor);
    }

    public void CreateIfDoesntExist()
    {
      m_Generator.Sync();
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

    public static bool TryChangeTermLaunchCmd((string, string) termLaunch)
    {
      (string cmd, string args) = termLaunch;

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
      EditorPrefs.SetString("NvimUnityTermLaunchCmd", cmd);
      EditorPrefs.SetString("NvimUnityTermLaunchArgs", args);

      return true;
    }

    private static CodeEditor.Installation[] s_DiscoveredNeovimInstallations;
    public CodeEditor.Installation[] Installations => s_DiscoveredNeovimInstallations;


    public NeovimCodeEditor(IGenerator projectGeneration)
    {
      m_Generator = projectGeneration;
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
      return !(object.Equals(installation, default(CodeEditor.Installation)));
    }


    // Unity calls this method when it populates "Preferences/External Tools"
    // in order to allow the code editor to generate necessary GUI. For example,
    // when creating an an argument field for modifying the arguments sent to
    // the code editor.
    public void OnGUI()
    {
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
        m_Generator.Sync();
      }
    }


    private void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip)
    {
      var prevValue = m_Generator.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
      var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
      if (newValue != prevValue)
      {
        m_Generator.AssemblyNameProvider.ToggleProjectGeneration(preference);
      }
    }

    // When you change Assets in Unity, this method for the current chosen
    // instance of IExternalCodeEditor parses the new and changed Assets.
    public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles,
        string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
    {
      m_Generator.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToList(),
          importedFiles);
    }


    // Unity calls this function during initialization in order to sync the
    // Project. This is different from SyncIfNeeded in that it does not get a
    // list of changes.
    public void SyncAll()
    {
      AssetDatabase.Refresh();
      m_Generator.Sync();
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
      string prevAddr = EditorPrefs.GetString("NvimPrevServerSocket");
      if (String.IsNullOrWhiteSpace(prevAddr)) return false;

      int idx = prevAddr.IndexOf(':');
      string ip = prevAddr.Substring(0, idx);
      int port = Int32.Parse(prevAddr.Substring(idx + 1));
      return NetUtils.IsPortInUse(ip, port);
#endif
    }


    // The external code editor needs to handle the request to open a file.
    // Note that by returning 'false' Unity will try to open the file in a different program which is
    // the reason why, for instance, we return 'false' for image files
    public bool OpenProject(string filePath = "", int line = -1, int column = -1)
    {
      if (!String.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath)) return false;
      if (line == -1) line = 1;
      if (column == -1) column = 0;

      // get terminal launch cmd and its args from Unity editor preferences
      string termLaunchCmd = EditorPrefs.GetString("NvimUnityTermLaunchCmd");
      string termLaunchArgs = EditorPrefs.GetString("NvimUnityTermLaunchArgs");

#if UNITY_EDITOR_LINUX
      string app = CodeEditor.CurrentEditorPath;
#else // UNITY_EDITOR_WIN
      string app = $"\"{CodeEditor.CurrentEditorPath}\"";
#endif

      // only use NeoVim for reasonable file extensions (e.g., do not use NeoVim to open .png files which happens
      // without this check)
      if (!Array.Exists(s_SupportedExtensions, e => e.ToLower() == Path.GetExtension(filePath)
            .TrimStart('.')
            .ToLower()))
        return false;

      if (!IsNvimServerInstanceAlreadyRunning())
      {
#if UNITY_EDITOR_WIN
        // on Windows, listening to a domain socket yields the following error: "neovim Failed to --listen: service not
        // available for socket type" so we have to listen to a TCP socket instead with a local addr and a random port
        s_ServerSocket = $"127.0.0.1:{NetUtils.GetRandomAvailablePort()}";
#endif

        try
        {
          using (Process p = new())
          {
            p.StartInfo.FileName = termLaunchCmd
              .Replace("{app}", app);
            p.StartInfo.Arguments = termLaunchArgs
              .Replace("{app}", app)
              .Replace("{filePath}", $"\"{filePath}\"")
              .Replace("{serverSocket}", s_ServerSocket);
            p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            p.StartInfo.CreateNoWindow = false;
            p.StartInfo.UseShellExecute = true;  // has to be true on Windows (irrelevant on Linux)

            // start and do not care (do not wait for exit)
            p.Start();

#if UNITY_EDITOR_WIN
            EditorPrefs.SetString("NvimPrevServerSocket", s_ServerSocket);
#endif
          }
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
      s_ServerSocket = EditorPrefs.GetString("NvimPrevServerSocket");
#endif

      // send request to Neovim server instance listening on the provided socket path to open a tab/buffer corresponding to the provided filepath
      {
        string args = EditorPrefs.GetString("NvimUnityOpenFileArgs")
          .Replace("{serverSocket}", s_ServerSocket)
          .Replace("{filePath}", $"\"{filePath}\"");
        try
        {
          ProcessUtils.RunProcessAndKillAfter(app, args, timeout: 50);
        } catch (Exception e)
        {
          Debug.LogWarning($"[neovim.ide] failed at sending request to Neovim server to open a file. cmd: {app} {args}. Reason: {e.Message}");
        }
      }

      // now send request to jump cursor to exact position. You cannot do both --remote-tab and --remote-send at the same time (I have no idea why.
      // You can do them together in a terminal but not through C# :-|).
      if (line != 1 || column != 0)
      {
        string args = EditorPrefs.GetString("NvimUnityJumpToCursorPositionArgs")
          .Replace("{serverSocket}", s_ServerSocket)
          .Replace("{line}", line.ToString())
          .Replace("{column}", column.ToString());
        try
        {
          ProcessUtils.RunProcessAndKillAfter(app, args, timeout: 50);
        }
        catch (Exception e)
        {
          Debug.LogWarning($"[neovim.ide] failed at jumping to cursor positions. cmd: {app} {args}. Reason: {e.Message}");
        }
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
            if (!ProcessUtils.RunShellCmd(cmd))
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
            if (!ProcessUtils.RunShellCmd(cmd))
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

#endif

      return true;
    }
  }
}
#endif
