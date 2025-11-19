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
    public static readonly (string, string) s_TermLaunchCmdTemplate = ("<terminal-emulator>", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocketPath}");

    // list of neovim launch cmds from popular terminal emulators - this is
    // just a hardcoded list so that non-tech-savy users can just get to
    // using Neovim with minimal friction.
    public static readonly (string, string)[] s_TermLaunchCmds =
#if UNITY_EDITOR_LINUX
    {
        ("gnome-terminal", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocketPath}"),
        ("alacritty", "--title \"nvimunity\" --command {app} {filePath} --listen {serverSocketPath}"),
        ("ptyxis", "--title \"nvimunity\" -- {app} {filePath} --listen {serverSocketPath}"),
        ("xterm", "-T \"nvimunity\" -e {app} {filePath} --listen {serverSocketPath}"),
    };
#else
    {
        ("{app}", "{filePath} --listen {serverSocketPath}"),  // run nvim.exe directly
        ("alacritty", "--title \"nvimunity\" --command {app} {filePath} --listen {serverSocketPath}"),
    };
#endif

    // Neovim installation paths on Linux here - the first valid path is picked otherwise the ENV variable TODO is
    // checked
    private static readonly string[] m_PossiblePaths =
#if UNITY_EDITOR_LINUX
     {
       "nvim",
       "/usr/bin/nvim",
       "/opt/nvim-linux64/bin/nvim",
     };
#else // UNITY_EDITOR_WIN
     // make sure to include the extension in the executalbe's name!
     {
       "nvim.exe",  // just to be safe - powershell bitches about missing .exe extension
       Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Path.Join("Neovim", "bin", "nvim.exe")),
     };
#endif

    private static string s_NvimExecutable = null;

    private IGenerator m_Generator = null;

    // because of the "InitializeOnLoad" attribute, this will be called when scripts in the project are recompiled
    static NeovimCodeEditor()
    {
      if (s_NvimExecutable == null)
      {
        try
        {
          s_NvimExecutable = m_PossiblePaths.First(path => File.Exists(path) || ProcessUtils.CheckCmdExistence($"\"{path}\""));
        }
        catch (InvalidOperationException)
        {
          Debug.LogError("[neovim.ide] failed to find a valid nvim executable");
          return;
        }
      }
      // get terminal launch cmd and its args from Unity editor preferences
      string termLaunchCmd = EditorPrefs.GetString("NvimUnityTermLaunchCmd");
      string termLaunchArgs = EditorPrefs.GetString("NvimUnityTermLaunchArgs");
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
#else
      // TODO: add auto Window focus on Windows platforms
      // on Windows, listening to a domain socket yields the following error:
      // "neovim Failed to --listen: service not available for socket type"
      // so we have to listen to a TCP socket instead with a local addr and
      // a random port - this will be overwitten below
      s_WindowFocusingAvailable = false;
#endif

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
          Debug.LogWarning("[neovim.ide] no terminal emulator is found. Please provide your own launch command by going to: "
              + "Neovim => ChangeTerminalLaunchCmd");
        }
      }

      NeovimCodeEditor editor = new(GeneratorFactory.GetInstance(GeneratorStyle.SDK));
      CodeEditor.Register(editor);
      editor.CreateIfDoesntExist();
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

      if (s_NvimExecutable != null)
        cmd = cmd.Replace("{app}", $"\"{s_NvimExecutable}\"");

      if (!ProcessUtils.CheckCmdExistence(cmd))
        return false;

      EditorPrefs.SetString("NvimUnityTermLaunchCmd", cmd);
      EditorPrefs.SetString("NvimUnityTermLaunchArgs", args);
      return true;
    }

    private CodeEditor.Installation[] m_Installations = null;
    public CodeEditor.Installation[] Installations
    {
      get
      {
        if (m_Installations != null) return m_Installations;
        try
        {
          m_Installations = new CodeEditor.Installation[]
          {
            new CodeEditor.Installation
            {
              Name = "Neovim",
              Path = s_NvimExecutable
            }
          };
        }
        catch (InvalidOperationException)
        {
          Debug.LogWarning("[neovim.ide] no Neovim executable (nvim) path was found. "
              + "Consider manually extending the \"m_PossiblePaths\" with your Neovim exeutable path.");
          m_Installations = new CodeEditor.Installation[] { };
        }
        return m_Installations;
      }
    }


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
      var lowerCasePath = editorPath.ToLower();
      var filename = Path.GetFileName(lowerCasePath).Replace(" ", "");
      var installations = Installations;

      if (!_supportedFileNames.Contains(filename))
      {
        installation = default;
        return false;
      }

      if (!installations.Any())
      {
        installation = new CodeEditor.Installation
        {
          Name = "Neovim",
          Path = editorPath
        };
      }
      else
      {
        try
        {
          installation = installations.First(inst => inst.Path == editorPath);
        }
        catch (InvalidOperationException)
        {
          installation = new CodeEditor.Installation
          {
            Name = "Neovim",
            Path = editorPath
          };
        }
      }

      return true;
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


    // The external code editor needs to handle the request to open a file.
    public bool OpenProject(string filePath = "", int line = -1, int column = -1)
    {
      if (!String.IsNullOrWhiteSpace(filePath) && !File.Exists(filePath)) return false;
      if (line == -1) line = 1;
      if (column == -1) column = 0;

      if (s_NvimExecutable == null) return false;

      // only use NeoVim for reasonable file extensions (e.g., do not use NeoVim to open .png files which happens
      // without this check)
      if (!Array.Exists(s_SupportedExtensions, e => e.ToLower() == Path.GetExtension(filePath).TrimStart('.').ToLower())) return false;

      // relying on the existance of a created neovim serer socket instance is not a good approach (not crossplatform,
      // issues, etc.) what we do instead is to store the PID of the create neovim server instance in EditorPrefs then
      // check whether we have a process of that ID
      int prevNvimServerPID = EditorPrefs.GetInt("NvimPrevServerPID", defaultValue: -1);
      Process nvimUnityProcess = null;
      try
      {
        nvimUnityProcess = Process.GetProcessById(prevNvimServerPID);
      }
      catch (ArgumentException) { }

      if (
        prevNvimServerPID == -1  // no previous nvim server process was created
        || nvimUnityProcess == null  // or, no matching PID was found
      )
      {
        // get terminal launch cmd and its args from Unity editor preferences
        string termLaunchCmd = EditorPrefs.GetString("NvimUnityTermLaunchCmd");
        string termLaunchArgs = EditorPrefs.GetString("NvimUnityTermLaunchArgs");
        if (String.IsNullOrWhiteSpace(termLaunchCmd) || String.IsNullOrWhiteSpace(termLaunchArgs))
        {
          Debug.LogError($"[neovim.ide] no valid terminal launcher is available. " +
              "You have to set the terminal launch command by going to the menu item: Neovim => ChangeTerminalLaunchCmd");
          return false;
        }

#if UNITY_EDITOR_WIN
        // on Windows, listening to a domain socket yields the following error: "neovim Failed to --listen: service not
        // available for socket type" so we have to listen to a TCP socket instead with a local addr and a random port
        s_ServerSocket = $"127.0.0.1:{NetUtils.GetRandomAvailablePort()}";
        EditorPrefs.SetString("NvimPrevServerSocket", s_ServerSocket);
#endif

        try
        {
          using (Process p = new())
          {
            p.StartInfo.FileName = termLaunchCmd
              .Replace("{app}", $"\"{s_NvimExecutable}\"");
            p.StartInfo.Arguments = termLaunchArgs
              .Replace("{app}", $"\"{s_NvimExecutable}\"")
              .Replace("{filePath}", $"\"{filePath}\"")
              .Replace("{serverSocketPath}", $"\"{s_ServerSocket}\"");
            p.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            p.StartInfo.CreateNoWindow = false;
            p.StartInfo.UseShellExecute = true;  // has to be true on Windows

            // start and do not care (do not wait for exit)
            p.Start();

            p.EnableRaisingEvents = true;
            p.Exited += new EventHandler(OnNvimServerInstanceExit);

            // save the PID so that in case the user exits Unity while keeping nvim open we can re-communicate with that
            // same nvim server instance
            EditorPrefs.SetInt("NvimPrevServerPID", p.Id);
          }
        }
        catch (Exception e)
        {
          Debug.LogError($"[neovim.ide] failed to create a Neovim server instance. Reason: {e}");
          return false;
        }
      }

#if UNITY_EDITOR_WIN
      s_ServerSocket = EditorPrefs.GetString("NvimPrevServerSocket");
#endif

      // send request to Neovim server instance listening on the provided socket path to open a tab/buffer corresponding to the provided filepath
      ProcessUtils.RunProcessAndExitImmediately($"\"{s_NvimExecutable}\"", $"--server \"{s_ServerSocket}\" --remote-tab \"{filePath}\"");

      // you cannot do both --remote-tab and --remote-send at the same time (I have no idea why. You can do them together in a terminal but not through C# :-|).
      ProcessUtils.RunProcessAndExitImmediately($"\"{s_NvimExecutable}\"", $"--server \"{s_ServerSocket}\" --remote-send \":call cursor({line},{column})<CR>\"");

      // optionally focus on Neovim - this is extremely tricky to implement across platform
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
#endif

      return true;
    }

    private void OnNvimServerInstanceExit(object sender, System.EventArgs e)
    {
      Debug.Log("unset");
      EditorPrefs.DeleteKey("NvimPrevServerPID");
    }
  }
}
#endif
