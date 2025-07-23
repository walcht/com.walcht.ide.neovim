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

      // add your Neovim installation path here
      // TODO: only use this if "nvim" command isn't already available in PATH
      private string[] m_PossiblePaths = { "/usr/bin/nvim", "/opt/nvim-linux64/bin/nvim" };
      private readonly string m_ServerSocketPath = "/tmp/nvimsocket";

      private IGenerator m_Generator = null;

      // because of the "InitializeOnLoad" attribute, this will be called when scripts in the project are recompiled
      static NeovimCodeEditor()
      {
          NeovimCodeEditor editor = new NeovimCodeEditor(GeneratorFactory.GetInstance(GeneratorStyle.SDK));
          CodeEditor.Register(editor);
          editor.CreateIfDoesntExist();
      }

      public void CreateIfDoesntExist()
      {
        m_Generator.Sync();
      }
  
  
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
          // AssetDatabase.Refresh();
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
              try
              {
                  string term = "gnome-terminal";
                  string args = $"--title \"nvimunity\" -- {app} {filePath} --listen {m_ServerSocketPath}";
  
                  using (Process serverProc = new Process())
                  {
                      serverProc.StartInfo.FileName = term;
                      serverProc.StartInfo.Arguments = args;
                      serverProc.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                      serverProc.StartInfo.CreateNoWindow = false;
                      serverProc.StartInfo.UseShellExecute = false;
                      serverProc.Start();
                      if (!serverProc.WaitForExit(500))
                      {
  #if DEBUG
                          Debug.LogError("[neovim.ide] failed at creating a Neovim server instance");
  #endif
                          return false;
                      }
                  }
              }
              catch (Exception e)
              {
  #if DEBUG
                  Debug.LogError($"[neovim.ide] failed at creating a Neovim server instance: {e.Message}");
  #endif
                  return false;
              }
          }
  
          string arguments = $"--server {m_ServerSocketPath} --remote-tab {filePath} --remote-send \":call cursor({line},{column})<CR>\"";
  
          try
          {
              using (Process proc = new Process())
              {
                  proc.StartInfo.FileName = app;
                  proc.StartInfo.Arguments = arguments;
                  proc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                  proc.StartInfo.CreateNoWindow = true;
                  proc.StartInfo.UseShellExecute = true;
                  proc.Start();
                  if (!proc.WaitForExit(500))
                  {
  #if DEBUG
                      Debug.LogError($"[neovim.ide] failed at sending request to open {filePath} to Neovim server instance " +
                          $"listening to {m_ServerSocketPath}");
  #endif
                      return false;
                  }
              }
          }
          catch (Exception e)
          {
  #if DEBUG
              Debug.LogError($"[neovim.ide] failed at sending request to open {filePath} to Neovim server instance " +
                  $"listening to {m_ServerSocketPath}. Reason: {e.Message}");
  #endif
              return false;
          }
  
          return true;
      }
  
  }
}
