#pragma warning disable IDE0130, IDE0090, IDE0066
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using SimpleJSON;

namespace Neovim.Editor
{

  public enum RoslynDiagnosticScope
  {
    none,
    openFiles,
    fullSolution,
  }

  public class ModifierBinding
  {
    /// <summary>
    /// EventModifiers cast to int (0 = no modifier = default).
    /// </summary>
    public int Modifiers = 0;


    /// <summary>
    /// String representation of this binding (e.g., "SHIFT+CTRL"). This is mainly used so that it is easier to read
    /// this from a raw JSON file.
    /// </summary>
    public string Representation = string.Empty;


    /// <summary>
    /// Arguments associated with this binding that will be supplied to nvim remote command.
    /// </summary>
    public string Args = string.Empty;
  }

  public class NeovimEditorConfig
  {
    // keep this defaulted to true
    private bool m_Dirty = true;

    public ProjectGenerationFlag m_CsprojFlags =
      ProjectGenerationFlag.BuiltIn |
      ProjectGenerationFlag.Embedded |
      ProjectGenerationFlag.Git |
      ProjectGenerationFlag.Local |
      ProjectGenerationFlag.LocalTarBall |
      ProjectGenerationFlag.Registry;

    /// <summary>
    /// Project generation flags (i.e., generate .csproj files for which packages/assets/projects).
    /// </summary>
    public ProjectGenerationFlag CsprojFlags
    {
      get => m_CsprojFlags;
      set
      {
        if (value == m_CsprojFlags)
          return;
        m_CsprojFlags = value;
        m_Dirty = true;
      }
    }

    private string m_NvimExecutablePath = string.Empty;

    /// <summary>
    /// Absolute path to the Neovim executable currently in use.
    /// </summary>
    public string NvimExecutablePath
    {
      get => m_NvimExecutablePath;
      set
      {
        if (value == m_NvimExecutablePath)
          return;
        m_NvimExecutablePath = value;
        m_Dirty = true;
      }
    }

    private int m_ProcessTimeout = 150;
    public int ProcessTimeout
    {
      get => m_ProcessTimeout;
      set
      {
        if (value == m_ProcessTimeout)
          return;
        m_ProcessTimeout = value;
        m_Dirty = true;
      }
    }

    private string m_TermLaunchCmd = string.Empty;
    public string TermLaunchCmd
    {
      get => m_TermLaunchCmd;
      set
      {
        if (value == m_TermLaunchCmd)
          return;
        m_TermLaunchCmd = value;
        m_Dirty = true;
      }
    }

    private string m_TermLaunchArgs = string.Empty;
    public string TermLaunchArgs
    {
      get => m_TermLaunchArgs;
      set
      {
        if (value == m_TermLaunchArgs)
          return;
        m_TermLaunchArgs = value;
        m_Dirty = true;
      }
    }

    private string m_TermLaunchEnv = string.Empty;
    public string TermLaunchEnv
    {
      get => m_TermLaunchEnv;
      set
      {
        if (value == m_TermLaunchEnv)
          return;
        m_TermLaunchEnv = value;
        m_Dirty = true;
      }
    }

    private string m_OpenFileArgs = string.Empty;
    /// <summary>
    /// Current open-file arguments that will be supplied to nvim remote cmd upon opening a file from Unity.
    /// </summary>
    public string OpenFileArgs
    {
      get => m_OpenFileArgs;
      set
      {
        if (value == m_OpenFileArgs)
          return;
        m_OpenFileArgs = value;
        m_Dirty = true;
      }
    }

    private List<ModifierBinding> m_ModifierBindings = new List<ModifierBinding>();
    public List<ModifierBinding> ModifierBindings
    {
      get => m_ModifierBindings;
      set
      {
        m_ModifierBindings = value;
        m_Dirty = true;
      }
    }

    private string m_JumpToCursorPositionArgs = string.Empty;
    public string JumpToCursorPositionArgs
    {
      get => m_JumpToCursorPositionArgs;
      set
      {
        if (value == m_JumpToCursorPositionArgs)
          return;
        m_JumpToCursorPositionArgs = value;
        m_Dirty = true;
      }
    }

    private string m_PrevServerSocket = string.Empty;
    public string PrevServerSocket
    {
      get => m_PrevServerSocket;
      set
      {
        if (value == m_PrevServerSocket)
          return;
        m_PrevServerSocket = value;
        m_Dirty = true;
      }
    }

#if UNITY_EDITOR_WIN
    private string m_PrevServerProcessIntPtrStringRepr = string.Empty;
    public string PrevServerProcessIntPtrStringRepr
    {
      get => m_PrevServerProcessIntPtrStringRepr;
      set
      {
        if (value == m_PrevServerProcessIntPtrStringRepr)
          return;
        m_PrevServerProcessIntPtrStringRepr = value;
        m_Dirty = true;
      }
    }
#endif

    private List<string> m_Analyzers = new List<string>();
    public List<string> Analyzers
    {
      get => m_Analyzers;
      set
      {
        m_Analyzers = value;
        m_Dirty = true;
      }
    }

    private RoslynDiagnosticScope m_analyzerDiagnosticScope = RoslynDiagnosticScope.openFiles;
    public RoslynDiagnosticScope AnalyzerDiagnosticScope
    {
      get => m_analyzerDiagnosticScope;
      set
      {
        if (value == m_analyzerDiagnosticScope)
          return;
        m_analyzerDiagnosticScope = value;
        m_Dirty = true;
      }
    }


    private RoslynDiagnosticScope m_compilerDiagnosticScope = RoslynDiagnosticScope.openFiles;
    public RoslynDiagnosticScope CompilerDiagnosticScope
    {
      get => m_compilerDiagnosticScope;
      set
      {
        if (value == m_compilerDiagnosticScope)
          return;
        m_compilerDiagnosticScope = value;
        m_Dirty = true;
      }
    }


    public bool SetDirty(bool dirty) => m_Dirty = dirty;


    public void Save()
    {

      if (!m_Dirty)
        return;

      string json;
      // create a JSONObject node and populate it
      {
        var node = new JSONObject();
        node.Add("AnalyzerDiagnosticScope", new JSONString(m_analyzerDiagnosticScope.ToString()));
        node.Add("CompilerDiagnosticScope", new JSONString(m_compilerDiagnosticScope.ToString()));
        node.Add("CsprojFlags", new JSONNumber((int)m_CsprojFlags));
        node.Add("NvimExecutablePath", new JSONString(m_NvimExecutablePath));
        node.Add("TermLaunchCmd", new JSONString(m_TermLaunchCmd));
        node.Add("TermLaunchArgs", new JSONString(m_TermLaunchArgs));
        node.Add("TermLaunchEnv", new JSONString(m_TermLaunchEnv));
        node.Add("OpenFileArgs", new JSONString(m_OpenFileArgs));
        node.Add("JumpToCursorPositionArgs", new JSONString(m_JumpToCursorPositionArgs));
        node.Add("ProcessTimeout", new JSONNumber(m_ProcessTimeout));
        node.Add("PrevServerSocket", new JSONString(m_PrevServerSocket));
#if UNITY_EDITOR_WIN
        node.Add("PrevServerProcessIntPtrStringRepr", new JSONString(m_PrevServerProcessIntPtrStringRepr));
#endif
        // analyzers
        {
          var nodeArray = new JSONArray();
          for (int i = 0; i < m_Analyzers.Count; ++i)
            nodeArray.Add(null, new JSONString(m_Analyzers[i]));
          node.Add("Analyzers", nodeArray);
        }

        // modifier bindings
        {
          var nodeArray = new JSONArray();
          for (int i = 0; i < m_ModifierBindings.Count; ++i)
          {
            var nodeObj = new JSONObject();
            nodeObj.Add("Modifiers", new JSONNumber(m_ModifierBindings[i].Modifiers));
            nodeObj.Add("Representation", new JSONString(m_ModifierBindings[i].Representation));
            nodeObj.Add("Args", new JSONString(m_ModifierBindings[i].Args));
            nodeArray.Add(null, nodeObj);
          }
          node.Add("ModifierBindings", nodeArray);
        }

        json = node.ToString();
      }
      EditorPrefs.SetString("NvimUnityConfigJson", json);
    }

    public static void Reset()
    {
      EditorPrefs.DeleteKey("NvimUnityConfigJson");
    }

    public static NeovimEditorConfig Load()
    {
      string json = EditorPrefs.GetString("NvimUnityConfigJson");

      // this means that there isn't any saved (i.e., persistent) config.
      // create a new config which should have some minimal default state
      // dirty flag is already set to true by default
      if (string.IsNullOrWhiteSpace(json))
      {
        var _config = new NeovimEditorConfig();
        _config.SetDirty(true);  // just to be 100% sure
        return _config;
      }

      var config = new NeovimEditorConfig();
      {
        var d = JSONNode.Parse(json) as JSONObject;

        Enum.TryParse<RoslynDiagnosticScope>((d["AnalyzerDiagnosticScope"] as JSONString).Value, out var analyzerDiagnosticScope);
        config.AnalyzerDiagnosticScope = analyzerDiagnosticScope;

        Enum.TryParse<RoslynDiagnosticScope>((d["CompilerDiagnosticScope"] as JSONString).Value, out var compilerDiagnosticScope);
        config.CompilerDiagnosticScope = compilerDiagnosticScope;

        config.CsprojFlags = (ProjectGenerationFlag)(d["CsprojFlags"] as JSONNumber).AsULong;
        config.NvimExecutablePath = (d["NvimExecutablePath"] as JSONString).Value;
        config.TermLaunchCmd = (d["TermLaunchCmd"] as JSONString).Value;
        config.TermLaunchArgs = (d["TermLaunchArgs"] as JSONString).Value;
        config.TermLaunchEnv = (d["TermLaunchEnv"] as JSONString).Value;
        config.OpenFileArgs = (d["OpenFileArgs"] as JSONString).Value;
        config.JumpToCursorPositionArgs = (d["JumpToCursorPositionArgs"] as JSONString).Value;
        config.ProcessTimeout = (int)(d["ProcessTimeout"] as JSONNumber).AsULong;
        config.PrevServerSocket = (d["PrevServerSocket"] as JSONString).Value;
#if UNITY_EDITOR_WIN
        config.PrevServerProcessIntPtrStringRepr = (d["PrevServerProcessIntPtrStringRepr"] as JSONString).Value;
#endif

        for (int i = 0; i < d["Analyzers"].Count; ++i)
        {
          config.Analyzers.Add(d["Analyzers"][i].Value);
        }

        for (int i = 0; i < d["ModifierBindings"].Count; ++i)
        {
          var mb = new ModifierBinding
          {
            Modifiers = (int)d["ModifierBindings"][i]["Modifiers"].AsULong,
            Representation = d["ModifierBindings"][i]["Representation"].Value,
            Args = d["ModifierBindings"][i]["Args"].Value
          };
          config.ModifierBindings.Add(mb);
        }
      }

      // since we have just deserialized this - it should not have an internal dirty state
      config.SetDirty(false);
      return config;

    }

    public bool TryAddAnalyzer(string path)
    {
      if (path != null && File.Exists(path) &&
          !m_Analyzers.Exists(analyzer => string.Compare(Path.GetFileName(analyzer),
              Path.GetFileName(path), StringComparison.OrdinalIgnoreCase) == 0))
      {
        m_Analyzers.Add(path);
        m_Dirty = true;
        return true;
      }
      return false;
    }

    public bool TryDelAnalyzer(string path)
    {
      if (m_Analyzers.Remove(path))
      {
        m_Dirty = true;
        return true;
      }
      return false;
    }

    public void DelAnalyzerAt(int idx)
    {
      m_Analyzers.RemoveAt(idx);
      m_Dirty = true;
    }

  }
}
