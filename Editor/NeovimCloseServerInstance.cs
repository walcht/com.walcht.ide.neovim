#if UNITY_EDITOR_LINUX || UNITY_EDITOR_WIN
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Neovim.Editor
{
  public class NeovimCloseServerInstance : EditorWindow
  {
    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    [MenuItem("Neovim/Close Neovim Server Instance")]
    static void Init()
    {
      int pid = EditorPrefs.GetInt("PrevNvimServerPID", defaultValue: -1);
      if (pid == -1)
      {
        Debug.Log("[neovim.ide] no running server instance was found");
        return;
      }

      EditorPrefs.DeleteKey("PrevNvimServerPID");

      Process p = null;
      try
      {
        p = Process.GetProcessById(pid);
      }
      catch (ArgumentException)
      {
        Debug.Log("[neovim.ide] no running server instance was found (was probably already closed)");
        return;
      }

      p.CloseMainWindow();
    }
  }

  public class ClearCurrentTerminalLaunchCommand : EditorWindow
  {
    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    [MenuItem("Neovim/Clear Current Terminal Launch Command")]
    static void Init()
    {
      EditorPrefs.DeleteKey("NvimUnityTermLaunchCmd");
      EditorPrefs.DeleteKey("NvimUnityTermLaunchArgs");
    }
  }
}
#endif
