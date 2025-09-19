using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
using Debug = UnityEngine.Debug;


namespace Neovim.Editor
{

  [Serializable]
  public class NeovimConfig
  {
#if UNITY_EDITOR_LINUX
    // the default launch cmd to use for launching a new Neovim instance
    // a GUI can overwrite this -- e.g., user enters a custom launch cmd)
    public string termLaunchCmd = null;
#endif
  }

  [InitializeOnLoad]
  public class NeovimCodeEditor : IExternalCodeEditor
  {
    static readonly string[] _supportedFileNames = { "nvim" };
    static readonly bool s_WindowFocusingAvailable = false;

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

    // terminal launch command template - use this template for adding new launch cmds
    public static readonly string s_TermLaunchCmdTemplate = "<terminal-emulator> --title \"nvimunity\" -- {app} {filePath} --listen {serverSocketPath}";

    // list of neovim launch cmds from popular terminal emulators - this is
    // just a hardcoded list so that non-tech-savy users can just get to
    // using Neovim with minimal friction.
    public static readonly List<string> s_TermLaunchCmds = new List<string>{
        "alacritty --title \"nvimunity\" --command {app} {filePath} --listen {serverSocketPath}",
        "gnome-terminal --title \"nvimunity\" -- {app} {filePath} --listen {serverSocketPath}",
        "ptyxis --title \"nvimunity\" -- {app} {filePath} --listen {serverSocketPath}",
        "xterm -T \"nvimunity\" -e {app} {filePath} --listen {serverSocketPath}",
      };

    private static NeovimConfig s_Config = new();
    public static string GetDefaultTermLaunchCmd() => s_Config.termLaunchCmd;

    public static readonly string s_ConfigPath = $"{Environment.GetEnvironmentVariable("HOME")}/.config/com.walcht.ide.neovim.json";

    // Neovim installation paths on Linux here
    private string[] m_PossiblePaths = { "/usr/bin/nvim", "/opt/nvim-linux64/bin/nvim" };

    // the location to create the server socket at
    private readonly string m_ServerSocketPath = "/tmp/nvimsocket";
#endif

    private IGenerator m_Generator = null;

    // because of the "InitializeOnLoad" attribute, this will be called when scripts in the project are recompiled
    static NeovimCodeEditor()
    {
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

      // if there is already a config file - get terminal launch cmd from there
      if (s_Config.termLaunchCmd == null && File.Exists(s_ConfigPath))
      {
        string termLaunchCmd = JsonUtility.FromJson<NeovimConfig>(File.ReadAllText(s_ConfigPath)).termLaunchCmd;
        ChangeTermLaunchCmd(termLaunchCmd);
      }
      // otherwise - use the `most common` terminal emulators list
      else if (s_Config.termLaunchCmd == null)
      {
        // pick the first default available terminal from the list of 'popular'
        // terminal emulators. Obviously this is some sort of a heuristic but
        // the user can explicitly change this through the GUI.
        foreach (string termLaunchCmd in s_TermLaunchCmds)
        {
          ChangeTermLaunchCmd(termLaunchCmd);
          if (s_Config.termLaunchCmd != null)
            break;
        }
        // no available terminal is found from the 'most common' term list
        if (s_Config.termLaunchCmd == null)
        {
          // you can't show a GUI window here -- so just log a warning
          Debug.LogWarning("[neovim.ide] no terminal emulator is found. Please provide your own launch command by going to: "
              + "Neovim => ChangeTerminalLaunchCmd");
        }
      }
#endif
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


    public static bool ChangeTermLaunchCmd(string termLaunchCmd)
    {
      if (String.IsNullOrWhiteSpace(termLaunchCmd) || !ProcessUtils.CheckCmdExistence(termLaunchCmd.Substring(0, termLaunchCmd.IndexOf(' '))))
        return false;

      s_Config.termLaunchCmd = termLaunchCmd;
      // and serialize to the config path
      File.WriteAllText(s_ConfigPath, JsonUtility.ToJson(s_Config, prettyPrint: true));
      return true;
    }
#endif

    private CodeEditor.Installation[] m_Installations = null;
    public CodeEditor.Installation[] Installations
    {
      get
      {
        if (m_Installations != null) return m_Installations;
        try
        {
          string path = m_PossiblePaths.First(path => new FileInfo(path).Exists);
          m_Installations = new CodeEditor.Installation[]
          {
            new CodeEditor.Installation
            {
              Name = "Neovim",
              Path = path
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
      if (filePath != "" && !File.Exists(filePath)) return false;
      if (line == -1) line = 1;
      if (column == -1) column = 0;

      string app = EditorPrefs.GetString("kScriptsDefaultApp");

      // if this exists then a Neovim server instance already exists
      if (!File.Exists(m_ServerSocketPath))
      {
#if UNITY_EDITOR_WIN
        // TODO: add support for Windows to launch a new neovim instance
#elif UNITY_EDITOR_LINUX
        if (s_Config.termLaunchCmd == null)
        {
          Debug.LogError($"[neovim.ide] no valid terminal launcher is available. " +
              "You have to set the terminal launch command by going to the menu item: Neovim => ChangeTerminalLaunchCmd");
          return false;
        }
        int spaceIdx = s_Config.termLaunchCmd.IndexOf(' ');
        string cmd = s_Config.termLaunchCmd.Substring(0, spaceIdx);
        string args = string.Format(s_Config.termLaunchCmd.Substring(spaceIdx + 1)
            .Replace("{app}", "{0}")
            .Replace("{filePath}", "{1}")
            .Replace("{serverSocketPath}", "{2}"), app, filePath, m_ServerSocketPath);
        if (!ProcessUtils.RunProcess(cmd, args, ProcessWindowStyle.Normal))
        {
          Debug.LogError($"[neovim.ide] failed at creating a Neovim server instance.");
          return false;
        }
#endif
      }

      // send request to Neovim server instance listening on the provided socket path to
      // open a tab/buffer corresponding to the provided filepath
      if (!ProcessUtils.RunProcess(app, $"--server {m_ServerSocketPath} --remote-tab {filePath}",
            ProcessWindowStyle.Hidden, createNoWindow: true))
      {
        Debug.LogError($"[neovim.ide] failed at sending a request to Neovim server instance to open file.");
        return false;
      }


      // you cannot do both --remote-tab and --remote-send at the same time (I have no idea why.
      // You can do them together in a terminal but not through C# :-|).
      if (!ProcessUtils.RunProcess(app, $"--server {m_ServerSocketPath} --remote-send \':call cursor({line},{column})<CR>\'",
            ProcessWindowStyle.Hidden, createNoWindow: true))
      {
        // it's fine if we fail here - we are just jumping to a line and cursor position
        Debug.LogWarning($"[neovim.ide] failed at sending request to jump to cursor position.");
      }

      // optionally focus on Neovim - this is extremely tricky to implement across platform
      if (!s_WindowFocusingAvailable)
        return true;
#if UNITY_EDITOR_WIN
      // TODO: add support for Windows to switch to Neovim window
#elif UNITY_EDITOR_LINUX
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
#elif UNITY_EDITOR_OSX
      // TODO: add support for MacOS to switch to Neovim window
#endif
      return true;
    }
  }
}
