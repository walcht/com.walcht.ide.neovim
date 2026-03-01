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
      container.style.padding = 10;

      // Kill Nvim on Quit section
      var killToggle = new Toggle("Kill Nvim on Quit")
      {
        tooltip = "When enabled, nvim server is killed when Unity closes. Default: off (preserves session across restarts).",
        value = NeovimCodeEditor.s_Config.KillNvimOnQuit
      };
      killToggle.RegisterValueChangedCallback(e =>
      {
        NeovimCodeEditor.s_Config.KillNvimOnQuit = e.newValue;
        NeovimCodeEditor.s_Config.Save();
      });
      container.Add(killToggle);

      container.Add(new Label { text = "", style = { height = 10 } });

      // Process Timeout section
      var timeoutLabel = new Label("Process Timeout (milliseconds)")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold }
      };
      container.Add(timeoutLabel);

      var timeoutField = new IntegerField
      {
        label = "Timeout",
        tooltip = "Process timeout after which the process is killed. Used for open-file, jump-to-cursor, and focus-on-neovim processes.",
        value = NeovimCodeEditor.s_Config.ProcessTimeout
      };
      container.Add(timeoutField);

      var timeoutHelp = new HelpBox(
        "Smaller values result in smoother experience at the cost of potential process being killed before completion.\n" +
        "Range: 1-1000ms",
        HelpBoxMessageType.Info
      );
      timeoutHelp.style.marginTop = 5;
      container.Add(timeoutHelp);

      var timeoutError = new Label { style = { color = Color.red, display = DisplayStyle.None } };
      container.Add(timeoutError);

      var updateTimeoutBtn = new Button(() =>
      {
        if (timeoutField.value <= 0)
        {
          timeoutError.text = "[ERROR] Cannot set timeout <= 0 (infinite timeout will freeze Unity Editor)";
          timeoutError.style.display = DisplayStyle.Flex;
          timeoutField.value = NeovimCodeEditor.s_Config.ProcessTimeout;
          return;
        }

        if (timeoutField.value > 1000)
        {
          timeoutError.text = "[ERROR] Cannot set timeout > 1000ms (will freeze Unity Editor)";
          timeoutError.style.display = DisplayStyle.Flex;
          timeoutField.value = NeovimCodeEditor.s_Config.ProcessTimeout;
          return;
        }

        NeovimCodeEditor.s_Config.ProcessTimeout = timeoutField.value;
        NeovimCodeEditor.s_Config.Save();
        timeoutError.style.display = DisplayStyle.None;
      })
      { text = "Update Timeout" };
      updateTimeoutBtn.style.marginTop = 5;
      container.Add(updateTimeoutBtn);
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
