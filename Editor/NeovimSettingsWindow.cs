#pragma warning disable IDE0130
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Neovim.Editor
{
  public class NeovimSettingsWindow : EditorWindow
  {
    private const string k_WindowTitle = "Neovim Settings";

    [MenuItem("Window/Neovim")]
    public static void ShowWindow()
    {
      var window = GetWindow<NeovimSettingsWindow>();
      window.titleContent = new GUIContent(k_WindowTitle);
      window.minSize = new Vector2(600, 400);
      window.position = new Rect(100, 100, 750, 550);
    }

    public void CreateGUI()
    {
      // TabView will be added here
    }
  }
}
