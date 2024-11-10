#define DEBUG

using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Unity.CodeEditor;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public class NeovimCodeEditor : IExternalCodeEditor
{
    string[] m_PossiblePaths = { "/usr/bin/nvim" };

    static readonly string[] _supportedFileNames = { "nvim" };
    private readonly string m_ServerSocketPath = "/tmp/nvimsocket";

    IGenerator _projectGeneration;

    static NeovimCodeEditor()
    {
        NeovimCodeEditor editor = new NeovimCodeEditor(new ProjectGeneration(
              Directory.GetParent(Application.dataPath).FullName));
        CodeEditor.Register(editor);
        editor.CreateIfDoesntExist();
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
#if DEBUG
                Debug.Log($"a Neovim executable path is found at: {path}");
#endif
                m_Installations = new CodeEditor.Installation[] { new CodeEditor.Installation
                {
                    Name = "Neovim",
                    Path = path
                }
                };
            }
            catch (InvalidOperationException)
            {
                Debug.LogWarning("no Neovim executable (nvim) path was found");
                m_Installations = new CodeEditor.Installation[] { };
            }
            return m_Installations;
        }
    }


    public NeovimCodeEditor(IGenerator projectGeneration)
    {
        _projectGeneration = projectGeneration;
    }

    // Callback to the IExternalCodeEditor when it has been chosen from the PreferenceWindow.
    public void Initialize(string editorInstallationPath) { }

    public void OnGUI()
    {
        EditorGUILayout.LabelField("Generate .csproj files for:");
        EditorGUI.indentLevel++;
        SettingsButton(ProjectGenerationFlag.Embedded, "Embedded packages", "");
        SettingsButton(ProjectGenerationFlag.Local, "Local packages", "");
        SettingsButton(ProjectGenerationFlag.Registry, "Registry packages", "");
        SettingsButton(ProjectGenerationFlag.Git, "Git packages", "");
        SettingsButton(ProjectGenerationFlag.BuiltIn, "Built-in packages", "");
#if UNITY_2019_3_OR_NEWER
    SettingsButton(ProjectGenerationFlag.LocalTarBall, "Local tarball", "");
#endif
        SettingsButton(ProjectGenerationFlag.Unknown, "Packages from unknown sources", "");
        RegenerateProjectFiles();
        EditorGUI.indentLevel--;
    }

    void RegenerateProjectFiles()
    {
        var rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(new GUILayoutOption[] { }));
        rect.width = 252;
        if (GUI.Button(rect, "Regenerate project files"))
        {
            _projectGeneration.Sync();
        }
    }

    void SettingsButton(ProjectGenerationFlag preference, string guiMessage, string toolTip)
    {
        var prevValue = _projectGeneration.AssemblyNameProvider.ProjectGenerationFlag.HasFlag(preference);
        var newValue = EditorGUILayout.Toggle(new GUIContent(guiMessage, toolTip), prevValue);
        if (newValue != prevValue)
        {
            _projectGeneration.AssemblyNameProvider.ToggleProjectGeneration(preference);
        }
    }

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
                string args = $"-- {app} {filePath} --listen {m_ServerSocketPath}";
#if DEBUG
                Debug.Log($"creating a Neovim server instance at {m_ServerSocketPath} using: {term} {args}");
#endif
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
                        Debug.LogError("failed at creating a Neovim server instance");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"failed at creating a Neovim server instance: {e.Message}");
                return false;
            }
        }

        string arguments = $"--server {m_ServerSocketPath} --remote-tab {filePath}";
#if DEBUG
        Debug.Log($"running shell command: {app} {arguments}");
#endif

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
                    Debug.LogError($"failed at sending request to open {filePath} to Neovim server instance " +
                        $"listening to {m_ServerSocketPath}");
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"failed at sending request to open {filePath} to Neovim server instance " +
                $"listening to {m_ServerSocketPath}. Reason: {e.Message}");
            return false;
        }


        try
        {
            using (Process cursorProc = new Process())
            {
                cursorProc.StartInfo.FileName = app;
                cursorProc.StartInfo.Arguments = $"--server {m_ServerSocketPath} --remote-send \":call cursor({line},{column})<CR>\"";
                cursorProc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                cursorProc.StartInfo.CreateNoWindow = true;
                cursorProc.StartInfo.UseShellExecute = true;
                cursorProc.Start();
                cursorProc.WaitForExit(500);
            }
        }
        catch (Exception)
        {
            // it's ok if this fails
        }

        return true;
    }

    public void SyncAll()
    {
        (_projectGeneration.AssemblyNameProvider as IPackageInfoCache)?.ResetPackageInfoCache();
        AssetDatabase.Refresh();
        _projectGeneration.Sync();
    }

    public void SyncIfNeeded(string[] addedFiles, string[] deletedFiles, string[] movedFiles, string[] movedFromFiles, string[] importedFiles)
    {
        (_projectGeneration.AssemblyNameProvider as IPackageInfoCache)?.ResetPackageInfoCache();
        _projectGeneration.SyncIfNeeded(addedFiles.Union(deletedFiles).Union(movedFiles).Union(movedFromFiles).ToList(), importedFiles);
    }

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

    public void CreateIfDoesntExist()
    {
        if (!_projectGeneration.SolutionExists())
        {
            _projectGeneration.Sync();
        }
    }

}
