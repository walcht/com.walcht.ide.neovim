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

    // File Opening tab working copy
    private List<ModifierBinding> m_Bindings;
    private Label m_BindingInfoName;
    private Label m_BindingInfoDesc;
    private VisualElement m_BindingRows;
    private static readonly List<string> s_TemplateNames;
    private const string k_CustomLabel = "Custom";

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
      // Use a ScrollView with foldout sections instead of TabView
      var scrollView = new ScrollView(ScrollViewMode.Vertical);
      scrollView.style.flexGrow = 1;

      var root = new VisualElement();
      root.style.flexDirection = FlexDirection.Column;
      root.style.paddingTop = 10;
      root.style.paddingBottom = 10;
      root.style.paddingLeft = 10;
      root.style.paddingRight = 10;

      scrollView.Add(root);

      // Behavior Section
      var behaviorFoldout = new Foldout { text = "Behavior Settings", value = true };
      root.Add(behaviorFoldout);
      var behaviorContent = new VisualElement();
      behaviorContent.style.marginLeft = 10;
      behaviorContent.style.marginBottom = 10;
      behaviorContent.style.paddingTop = 5;
      root.Add(behaviorContent);
      behaviorFoldout.RegisterValueChangedCallback(e => behaviorContent.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None);
      CreateBehaviorSection(behaviorContent);

      // Terminal Section
      var terminalFoldout = new Foldout { text = "Terminal Launch Command", value = false };
      root.Add(terminalFoldout);
      var terminalContent = new VisualElement();
      terminalContent.style.marginLeft = 10;
      terminalContent.style.marginBottom = 10;
      terminalContent.style.paddingTop = 5;
      terminalContent.style.display = DisplayStyle.None;
      root.Add(terminalContent);
      terminalFoldout.RegisterValueChangedCallback(e => terminalContent.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None);
      CreateTerminalSection(terminalContent);

      // File Opening Section
      var fileOpeningFoldout = new Foldout { text = "File Opening Settings", value = false };
      root.Add(fileOpeningFoldout);
      var fileOpeningContent = new VisualElement();
      fileOpeningContent.style.marginLeft = 10;
      fileOpeningContent.style.marginBottom = 10;
      fileOpeningContent.style.paddingTop = 5;
      fileOpeningContent.style.display = DisplayStyle.None;
      root.Add(fileOpeningContent);
      fileOpeningFoldout.RegisterValueChangedCallback(e => fileOpeningContent.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None);
      CreateFileOpeningSection(fileOpeningContent);

      // Maintenance Section
      var maintenanceFoldout = new Foldout { text = "Server Maintenance", value = false };
      root.Add(maintenanceFoldout);
      var maintenanceContent = new VisualElement();
      maintenanceContent.style.marginLeft = 10;
      maintenanceContent.style.marginBottom = 10;
      maintenanceContent.style.paddingTop = 5;
      maintenanceContent.style.display = DisplayStyle.None;
      root.Add(maintenanceContent);
      maintenanceFoldout.RegisterValueChangedCallback(e => maintenanceContent.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None);
      CreateMaintenanceSection(maintenanceContent);

      rootVisualElement.Add(scrollView);
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

      // Current values display (read-only for now, can be changed via menu if needed)
      var cmdLabel = new Label("Command:");
      container.Add(cmdLabel);
      var cmdField = new TextField { value = NeovimCodeEditor.s_Config.TermLaunchCmd ?? "", isReadOnly = true };
      container.Add(cmdField);

      var argsLabel = new Label("Arguments:");
      container.Add(argsLabel);
      var argsField = new TextField { value = NeovimCodeEditor.s_Config.TermLaunchArgs ?? "", isReadOnly = true };
      container.Add(argsField);

      var envLabel = new Label("Environment Variables:");
      container.Add(envLabel);
      var envField = new TextField { value = NeovimCodeEditor.s_Config.TermLaunchEnv ?? "", isReadOnly = true };
      container.Add(envField);

      container.Add(new VisualElement { style = { height = 10 } });

      var helpMsg = new HelpBox(
        "To change terminal settings, use: Neovim → Change Terminal Launch Cmd\n\n" +
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
      container.Add(helpMsg);
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

      // RIGHT COLUMN: Jump args
      var rightColumn = new VisualElement
      {
        style = { width = 300, flexDirection = FlexDirection.Column }
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

      // Separator
      container.Add(new VisualElement { style = { height = 20 } });

      // Regenerate Project Files button
      var regenTitle = new Label("Project Generation")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14 }
      };
      container.Add(regenTitle);

      var regenHelp = new HelpBox(
        "Regenerate .csproj files for the current project.",
        HelpBoxMessageType.None
      );
      regenHelp.style.marginTop = 5;
      container.Add(regenHelp);

      var regenBtn = new Button(() =>
      {
        AssetDatabase.Refresh();
        Debug.Log("[Neovim Settings] Project regeneration triggered.");
      })
      { text = "Regenerate Project Files" };
      regenBtn.style.marginTop = 10;
      container.Add(regenBtn);
    }
  }
}
