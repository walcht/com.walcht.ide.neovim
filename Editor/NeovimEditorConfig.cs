#pragma warning disable IDE0130
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
// using Debug = UnityEngine.Debug;
using Newtonsoft.Json;

namespace Neovim.Editor
{
  [Serializable]
  public class ModifierBinding
  {
    /// <summary>
    /// EventModifiers cast to int (0 = no modifier = default).
    /// </summary>
    public int Modifiers;


    /// <summary>
    /// String representation of this binding (e.g., "SHIFT+CTRL"). This is mainly used so that it is easier to read
    /// this from a raw JSON file.
    /// </summary>
    public string Representation;


    /// <summary>
    /// Arguments associated with this binding that will be supplied to nvim remote command.
    /// </summary>
    public string Args;
  }

  [Serializable]
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

    private string m_NvimExecutablePath;

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

    private string m_TermLaunchCmd;
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

    private string m_TermLaunchArgs;
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

    private string m_TermLaunchEnv;
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

    private string m_OpenFileArgs;
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

    private List<ModifierBinding> m_ModifierBindings = new();
    public List<ModifierBinding> ModifierBindings
    {
      get => m_ModifierBindings;
      set
      {
        m_ModifierBindings = value;
        m_Dirty = true;
      }
    }

    private string m_JumpToCursorPositionArgs;
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

    private string m_PrevServerSocket;
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
    private string m_PrevServerProcessIntPtrStringRepr;
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

    private List<string> m_Analyzers = new();
    public List<string> Analyzers
    {
      get => m_Analyzers;
      set
      {
        m_Analyzers = value;
        m_Dirty = true;
      }
    }

    public bool SetDirty(bool dirty) => m_Dirty = dirty;

    public void Save()
    {
      if (!m_Dirty)
        return;
      string json = JsonConvert.SerializeObject(this /* Formatting.Indented */);
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
        var config = new NeovimEditorConfig();
        config.SetDirty(true);  // just to be 100% sure
        return config;
      }

      var neovimConfig = JsonConvert.DeserializeObject<NeovimEditorConfig>(json);
      // since we have just deserialized this - it should not have an internal dirty state
      neovimConfig.SetDirty(false);
      return neovimConfig;
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
