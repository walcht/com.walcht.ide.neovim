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
      var tabView = new TabView();
      tabView.style.flexGrow = 1;

      // Tab 1: Behavior
      var behaviorTab = new VisualElement();
      behaviorTab.name = "BehaviorTab";
      CreateBehaviorTab(behaviorTab);
      tabView.AddTab("Behavior", behaviorTab);

      // Tab 2: Terminal
      var terminalTab = new VisualElement();
      terminalTab.name = "TerminalTab";
      CreateTerminalTab(terminalTab);
      tabView.AddTab("Terminal", terminalTab);

      // Tab 3: File Opening
      var fileOpeningTab = new VisualElement();
      fileOpeningTab.name = "FileOpeningTab";
      CreateFileOpeningTab(fileOpeningTab);
      tabView.AddTab("File Opening", fileOpeningTab);

      // Tab 4: Maintenance
      var maintenanceTab = new VisualElement();
      maintenanceTab.name = "MaintenanceTab";
      CreateMaintenanceTab(maintenanceTab);
      tabView.AddTab("Maintenance", maintenanceTab);

      rootVisualElement.Add(tabView);
    }

    private void CreateBehaviorTab(VisualElement container)
    {
      // Will be implemented in Task 3
    }

    private void CreateTerminalTab(VisualElement container)
    {
      // Will be implemented in Task 4
    }

    private void CreateFileOpeningTab(VisualElement container)
    {
      // Will be implemented in Task 5
    }

    private void CreateMaintenanceTab(VisualElement container)
    {
      // Will be implemented in Task 6
    }
  }
}
