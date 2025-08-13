// uncomment for debugging messages
// #define DEBUG
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
using Debug = UnityEngine.Debug;


namespace Neovim.Editor
{
  [InitializeOnLoad]
  public class NeovimCodeEditor : IExternalCodeEditor
  {
      static readonly string[] _supportedFileNames = { "nvim" };

#if UNITY_EDITOR_LINUX
      private enum LinuxDesktopEnvironment {
        GNOME,
        KDE,
        OTHER,
        UNKNOWN,
      }
      private static readonly LinuxDesktopEnvironment s_LinuxPlatform;
#endif

      // add your Neovim installation path here
      private string[] m_PossiblePaths = { "/usr/bin/nvim", "/opt/nvim-linux64/bin/nvim" };
      private readonly string m_ServerSocketPath = "/tmp/nvimsocket";

      private IGenerator m_Generator = null;

      // because of the "InitializeOnLoad" attribute, this will be called when scripts in the project are recompiled
      static NeovimCodeEditor()
      {
#if UNITY_EDITOR_LINUX
          s_LinuxPlatform = DetermineLinuxDesktopEnvironment();
          if (s_LinuxPlatform == LinuxDesktopEnvironment.GNOME)
          {
            RunShellCmd(@"
UUID=activate-window-by-title@lucaswerkmeister.de
if ! gnome-extensions list | grep --quiet $UUID; then
  busctl --user call org.gnome.Shell.Extensions /org/gnome/Shell/Extensions org.gnome.Shell.Extensions InstallRemoteExtension s $UUID
fi
", timeout: 10000);
          }
#endif
          NeovimCodeEditor editor = new NeovimCodeEditor(GeneratorFactory.GetInstance(GeneratorStyle.SDK));
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

      private static bool RunShellCmd(string cmd, int timeout = 500)
      {
        bool success = false;
        string escapedArgs = escapedArgs = cmd.Replace("\"", "\\\"");
        try
        {
          using (Process p = new ())
          {
            p.StartInfo.FileName = "/bin/bash";
            p.StartInfo.Arguments = $"-c \"{escapedArgs}\"";
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            if (!p.WaitForExit(timeout))
            {
              success = false;
            } else
            {
              success = (p.ExitCode == 0);
            }
          }
        }
        catch (Exception)
        {
          success = false;
        }
        return success;
      }
#endif

      private static bool RunProcess(string app, string args, ProcessWindowStyle winStyle,
          bool createNoWindow = false, int timeout = 500)
      {
        bool success = false;
        try
        {
          using (Process p = new ())
          {
            p.StartInfo.FileName = app;
            p.StartInfo.Arguments = args;
            p.StartInfo.WindowStyle = winStyle;
            p.StartInfo.CreateNoWindow = createNoWindow;
            p.StartInfo.UseShellExecute = false;
            p.Start();
            if (!p.WaitForExit(timeout))
            {
              success = false;
            } else
            {
              success = (p.ExitCode == 0);
            }
          }
        }
        catch (Exception)
        {
          success = false;
        }
        return success;
      }


#if UNITY_EDITOR_LINUX
#endif
  
      private CodeEditor.Installation[] m_Installations = null;
      public CodeEditor.Installation[] Installations
      {
          get
          {
              if (m_Installations != null) return m_Installations;
              try
              {
                  string path = m_PossiblePaths.First(path => (new FileInfo(path)).Exists);
  
                  m_Installations = new CodeEditor.Installation[] { new CodeEditor.Installation
                    {
                        Name = "Neovim",
                        Path = path
                    }
                  };
              }
              catch (InvalidOperationException)
              {
  #if DEBUG
                  Debug.LogWarning("[neovim.ide] no Neovim executable (nvim) path was found. "
                      + "Consider manually extending the \"m_PossiblePaths\" with your Neovim exeutable path.");
  #endif
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
#if DEBUG
            Debug.Log($"launching new Neovim instance server instance ...");
#endif
            if (!RunProcess("gnome-terminal", $"--title \"nvimunity\" -- {app} {filePath} --listen {m_ServerSocketPath}",
                  ProcessWindowStyle.Normal))
            {
#if DEBUG
              Debug.LogError($"[neovim.ide] failed at creating a Neovim server instance.");
#endif
              return false;
            }
          }

#if DEBUG
          Debug.Log($"[neovim.ide] sending request to Neovim server instance at: {m_ServerSocketPath} to open file at: {filePath} ...");
#endif
          if (!RunProcess(app, $"--server {m_ServerSocketPath} --remote-tab {filePath}",
                ProcessWindowStyle.Hidden, createNoWindow: true))
          {
#if DEBUG
              Debug.LogError($"[neovim.ide] failed at sending a request to Neovim server instance to open file.");
#endif
              return false;
          }


          // you cannot do both --remote-tab and --remote-send at the same time (I have no idea why.
          // You can do them together in a terminal but through C#, nope).
#if DEBUG
          Debug.Log($"[neovim.ide] sending request to Neovim to jump to position: cursor position: line={line} column={column}");
#endif
          if (!RunProcess(app, $"--server {m_ServerSocketPath} --remote-send \':call cursor({line},{column})<CR>\'",
                ProcessWindowStyle.Hidden, createNoWindow: true))
          {
            // it's fine if we fail here - we are just jumping to a line and cursor position
#if DEBUG
            Debug.LogWarning($"[neovim.ide] failed at sending request to jump to cursor position.");
#endif
          }

          // optionally focus on Neovim - this is very tricky to implement cross platform
#if UNITY_EDITOR_WIN
          // TODO: add support for Windows to switch to Neovim window
#elif UNITY_EDITOR_LINUX
#if DEBUG
          Debug.Log($"[neovim.ide] focusing on Neovim server instance window on {s_LinuxPlatform} ...");
#endif
          switch (s_LinuxPlatform)
          {
            case LinuxDesktopEnvironment.GNOME:
              {
                string cmd = @"gdbus call --session \
    --dest org.gnome.Shell \
    --object-path /de/lucaswerkmeister/ActivateWindowByTitle \
    --method de.lucaswerkmeister.ActivateWindowByTitle.activateBySubstring \
    'nvimunity'";
                if (!RunShellCmd(cmd))
                {
#if DEBUG
                  Debug.LogWarning($"[neovim.ide] failed to focus on Neovim server instance titled 'nvimunity'.\n"
                      + "Did you logout and login of your GNOME session?");
#endif
                }
              }
              break;
            case LinuxDesktopEnvironment.KDE:
              {
                // TODO: add support for switching focus to Neovim on KDE
              }
              break;
            default:
              {
                // TODO: add support for switching focus to Neovim on other Linux desktop environments
              }
              break;
          }
#elif UNITY_EDITOR_OSX
          // TODO: add support for MacOS to switch to Neovim window
#endif
          return true;
      }
  
  }
}
