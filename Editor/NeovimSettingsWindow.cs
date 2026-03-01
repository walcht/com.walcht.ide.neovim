#pragma warning disable IDE0130
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;
using System.Collections.Generic;

namespace Neovim.Editor
{
  public class NeovimSettingsWindow : EditorWindow
  {
    private const string k_WindowTitle = "Neovim Settings";

    // Tab tracking
    private enum Tab { Behavior, Terminal, FileOpening, Maintenance }
    private Tab m_CurrentTab = Tab.Behavior;

    // File Opening tab working copy
    private List<ModifierBinding> m_Bindings;
    private Label m_BindingInfoName;
    private Label m_BindingInfoDesc;
    private VisualElement m_BindingRows;
    private static readonly List<string> s_TemplateNames;
    private const string k_CustomLabel = "Custom";

    // Tab content containers
    private VisualElement m_TabContentContainer;
    private VisualElement m_BehaviorContent;
    private VisualElement m_TerminalContent;
    private VisualElement m_FileOpeningContent;
    private VisualElement m_MaintenanceContent;

    static NeovimSettingsWindow()
    {
      s_TemplateNames = NeovimCodeEditor.s_OpenFileArgsTemplates
        .Select(t => t.Name)
        .Append(k_CustomLabel)
        .ToList();
    }

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
      // Create root container
      var root = new VisualElement();
      root.style.flexDirection = FlexDirection.Column;
      root.style.flexGrow = 1;
      rootVisualElement.Add(root);

      // Create tab toolbar
      var toolbar = new Toolbar();
      toolbar.style.height = 30;
      root.Add(toolbar);

      // Create tab buttons
      CreateTabButton(toolbar, "Behavior", Tab.Behavior, m_CurrentTab == Tab.Behavior);
      CreateTabButton(toolbar, "Terminal", Tab.Terminal, m_CurrentTab == Tab.Terminal);
      CreateTabButton(toolbar, "File Opening", Tab.FileOpening, m_CurrentTab == Tab.FileOpening);
      CreateTabButton(toolbar, "Maintenance", Tab.Maintenance, m_CurrentTab == Tab.Maintenance);

      // Tab content container
      m_TabContentContainer = new VisualElement();
      m_TabContentContainer.style.flexGrow = 1;
      m_TabContentContainer.style.paddingTop = 10;
      m_TabContentContainer.style.paddingBottom = 10;
      m_TabContentContainer.style.paddingLeft = 10;
      m_TabContentContainer.style.paddingRight = 10;
      root.Add(m_TabContentContainer);

      // Create tab content containers
      m_BehaviorContent = CreateTabContentContainer();
      m_TerminalContent = CreateTabContentContainer();
      m_FileOpeningContent = CreateTabContentContainer();
      m_MaintenanceContent = CreateTabContentContainer();

      // Populate tab contents
      CreateBehaviorSection(m_BehaviorContent);
      CreateTerminalSection(m_TerminalContent);
      CreateFileOpeningSection(m_FileOpeningContent);
      CreateMaintenanceSection(m_MaintenanceContent);

      // Show initial tab
      ShowTab(m_CurrentTab);
    }

    private VisualElement CreateTabContentContainer()
    {
      var container = new VisualElement();
      container.style.flexGrow = 1;
      container.style.display = DisplayStyle.None;
      return container;
    }

    private void CreateTabButton(Toolbar parent, string label, Tab tab, bool isSelected)
    {
      var button = new Button(() => ShowTab(tab))
      {
        text = label,
        style =
        {
          flexGrow = 1,
          unityFontStyleAndWeight = isSelected ? FontStyle.Bold : FontStyle.Normal,
          backgroundColor = isSelected ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.2f, 0.2f, 0.2f)
        }
      };
      button.AddToClassList("tab-button");
      parent.Add(button);
    }

    private void ShowTab(Tab tab)
    {
      m_CurrentTab = tab;

      // Hide all tab contents
      m_BehaviorContent.style.display = DisplayStyle.None;
      m_TerminalContent.style.display = DisplayStyle.None;
      m_FileOpeningContent.style.display = DisplayStyle.None;
      m_MaintenanceContent.style.display = DisplayStyle.None;

      // Show selected tab content
      switch (tab)
      {
        case Tab.Behavior:
          m_BehaviorContent.style.display = DisplayStyle.Flex;
          break;
        case Tab.Terminal:
          m_TerminalContent.style.display = DisplayStyle.Flex;
          break;
        case Tab.FileOpening:
          m_FileOpeningContent.style.display = DisplayStyle.Flex;
          break;
        case Tab.Maintenance:
          m_MaintenanceContent.style.display = DisplayStyle.Flex;
          break;
      }

      // Recreate GUI to update button states
      rootVisualElement.Clear();
      CreateGUI();
    }

    private void CreateBehaviorSection(VisualElement container)
    {
      // Kill Nvim on Quit
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

      container.Add(new VisualElement { style = { height = 10 } });

      // Process Timeout
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

    private void CreateTerminalSection(VisualElement container)
    {
      // Header
      var header = new Label("Terminal Launch Command")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold }
      };
      container.Add(header);

      // Template dropdown
      var templateNames = NeovimCodeEditor.s_TermLaunchCmds
        .Select((t, _) => t.Item1)
        .Append("Custom")
        .ToList();
      var templateDropdown = new DropdownField("Template", templateNames, 0);
      templateDropdown.SetValueWithoutNotify("Select template...");
      templateDropdown.style.marginTop = 5;
      container.Add(templateDropdown);

      // Command field
      var cmdField = new TextField
      {
        label = "Command",
        tooltip = "Executable that will be executed when opening a file for the first time. Must be accessible via PATH.",
        value = NeovimCodeEditor.s_Config.TermLaunchCmd ?? ""
      };
      container.Add(cmdField);

      // Arguments field
      var argsField = new TextField
      {
        label = "Arguments",
        tooltip = "Arguments passed to the executable. Use {app}, {filePath}, {serverSocket}, {instanceId} as placeholders.",
        value = NeovimCodeEditor.s_Config.TermLaunchArgs ?? ""
      };
      container.Add(argsField);

      // Environment field
      var envField = new TextField
      {
        label = "Environment Variables",
        tooltip = "Space-separated list: ENV_0=VALUE_0 ENV_1=VALUE_1",
        value = NeovimCodeEditor.s_Config.TermLaunchEnv ?? ""
      };
      container.Add(envField);

      // Placeholder reference
      var placeholderHelp = new HelpBox(
        "Placeholders:\n" +
        "{app} - Neovim executable path\n" +
        "{filePath} - Path to file being opened\n" +
        "{serverSocket} - Socket for Neovim server communication\n" +
        "{instanceId} - Unity process ID\n" +
#if UNITY_EDITOR_WIN
        "{getProcessPPIDScriptPath} - Path to GetProcessPPID.ps1 for window focusing\n" +
#endif
        "{environment} - Environment variables from the field above",
        HelpBoxMessageType.Info
      );
      placeholderHelp.style.marginTop = 5;
      container.Add(placeholderHelp);

      // Status message
      var statusLabel = new Label { style = { color = Color.green, marginTop = 5 } };
      container.Add(statusLabel);

      // Template dropdown callback
      templateDropdown.RegisterValueChangedCallback(e =>
      {
        if (e.newValue == "Custom") return;
        var template = NeovimCodeEditor.s_TermLaunchCmds.First(t => t.Item1 == e.newValue);
        cmdField.value = template.Item1;
        argsField.value = template.Item2;
        envField.value = template.Item3;
      });

      // Update button
      var updateBtn = new Button(() =>
      {
        if (!NeovimCodeEditor.TryChangeTermLaunchCmd((cmdField.value, argsField.value, envField.value)))
        {
          statusLabel.text = "[ERROR] Terminal not available. Check if command exists in PATH.";
          statusLabel.style.color = Color.red;
        }
        else
        {
          statusLabel.text = "[INFO] Terminal launch command updated successfully.";
          statusLabel.style.color = Color.green;
        }
      })
      { text = "Update" };
      updateBtn.style.marginTop = 5;
      container.Add(updateBtn);
    }

    private void CreateFileOpeningSection(VisualElement container)
    {
      // Two-column layout: info left, jump args right
      var twoColumn = new VisualElement();
      twoColumn.style.flexDirection = FlexDirection.Row;
      container.Add(twoColumn);

      // LEFT COLUMN: Info
      var leftColumn = new VisualElement
      {
        style = { flexGrow = 1, flexDirection = FlexDirection.Column, paddingRight = 10 }
      };
      twoColumn.Add(leftColumn);

      var infoTitle = new Label("Open-File Request Args")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold }
      };
      leftColumn.Add(infoTitle);

      var infoMsg = new HelpBox(
        "To configure modifier-based file opening behavior (e.g., Shift+Click to open in new tab),\n" +
        "use: Neovim → Change Open-File Request Args",
        HelpBoxMessageType.Info
      );
      leftColumn.Add(infoMsg);

      // RIGHT COLUMN: Jump args - 50% wider (was 300, now 450)
      var rightColumn = new VisualElement
      {
        style = { width = 450, flexDirection = FlexDirection.Column }
      };
      twoColumn.Add(rightColumn);

      var jumpTitle = new Label("Jump-to-Cursor Arguments")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold }
      };
      rightColumn.Add(jumpTitle);

      var jumpField = new TextField
      {
        label = "Arguments",
        tooltip = "Arguments when jumping to a specific line/column in Neovim.",
        value = NeovimCodeEditor.s_Config.JumpToCursorPositionArgs
      };
      rightColumn.Add(jumpField);

      var jumpHelp = new Label(
        "Placeholders:\n{serverSocket} - Socket for Neovim communication\n{line} - Line number\n{column} - Column number"
      )
      {
        style = { fontSize = 10, whiteSpace = WhiteSpace.Normal, marginTop = 5 }
      };
      rightColumn.Add(jumpHelp);

      var updateJumpBtn = new Button(() =>
      {
        NeovimCodeEditor.s_Config.JumpToCursorPositionArgs = jumpField.value;
        NeovimCodeEditor.s_Config.Save();
      })
      { text = "Update" };
      updateJumpBtn.style.marginTop = 5;
      rightColumn.Add(updateJumpBtn);
    }

    private void CreateMaintenanceSection(VisualElement container)
    {
      var title = new Label("Server Management")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14 }
      };
      container.Add(title);

      var helpBox = new HelpBox(
        "Use these buttons if nvim becomes unresponsive or Unity crashes.",
        HelpBoxMessageType.Info
      );
      helpBox.style.marginTop = 5;
      container.Add(helpBox);

      container.Add(new VisualElement { style = { height = 15 } });

      // Button row
      var buttonRow = new VisualElement
      {
        style = { flexDirection = FlexDirection.Row }
      };
      container.Add(buttonRow);

      var killBtn = new Button(() =>
      {
        NeovimCodeEditor.KillOrphanedServer();
        Debug.Log("[Neovim Settings] Kill Orphaned Server executed.");
      })
      { text = "Kill Orphaned Server" };
      killBtn.style.flexGrow = 1;
      killBtn.style.marginRight = 5;
      buttonRow.Add(killBtn);

      var resetBtn = new Button(() =>
      {
        NeovimCodeEditor.ResetConfig();
        Debug.Log("[Neovim Settings] Reset Config executed.");
      })
      { text = "Reset Config" };
      resetBtn.style.flexGrow = 1;
      buttonRow.Add(resetBtn);

      // Force Reset button
      var forceResetBtn = new Button(() =>
      {
        NeovimCodeEditor.KillOrphanedServer();
        NeovimCodeEditor.ResetConfig();
        Debug.Log("[Neovim Settings] Force Reset executed.");
      })
      { text = "Force Reset (Kill + Reset)" };
      forceResetBtn.style.marginTop = 10;
      container.Add(forceResetBtn);

      // NOTE: Regenerate Project Files removed - already exists in External Tools preferences
    }
  }
}
