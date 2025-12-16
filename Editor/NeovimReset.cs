#pragma warning disable IDE0130
using UnityEditor;
using UnityEngine;

namespace Neovim.Editor
{
  public class NeovimReset : EditorWindow
  {
    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    [MenuItem("Neovim/Reset")]
    static void Init()
    {
      EditorPrefs.DeleteKey("NvimUnityConfigJson");
      NeovimCodeEditor.InitConfig();
      Debug.Log("[neovim.ide] reset the previously saved neovim config");
    }
  }
}
