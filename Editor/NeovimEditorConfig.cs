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
  public class NeovimEditorConfig
  {
    private bool m_Dirty = false;

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

    private string m_OpenFileArgs;
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

    public static NeovimEditorConfig Load()
    {
      string json = EditorPrefs.GetString("NvimUnityConfigJson");
      if (string.IsNullOrWhiteSpace(json))
        return new();

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
