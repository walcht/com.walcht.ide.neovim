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
    private const string k_CustomLabel = "Custom";

    // File Opening section state
    private List<ModifierBinding> m_Bindings;
    private VisualElement m_BindingRows;
    private Label m_InfoName;
    private Label m_InfoDesc;

    private static readonly List<string> s_TemplateNames;

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
      // Create TabView
      var tabView = new TabView();
      tabView.style.flexGrow = 1;

      // Tab 1: Behavior
      var behaviorTab = new Tab("Behavior");
      var behaviorContent = new VisualElement();
      behaviorContent.style.paddingTop = 10;
      behaviorContent.style.paddingBottom = 10;
      behaviorContent.style.paddingLeft = 10;
      behaviorContent.style.paddingRight = 10;
      CreateBehaviorSection(behaviorContent);
      behaviorTab.Add(behaviorContent);
      tabView.Add(behaviorTab);

      // Tab 2: Terminal
      var terminalTab = new Tab("Terminal");
      var terminalContent = new VisualElement();
      terminalContent.style.paddingTop = 10;
      terminalContent.style.paddingBottom = 10;
      terminalContent.style.paddingLeft = 10;
      terminalContent.style.paddingRight = 10;
      CreateTerminalSection(terminalContent);
      terminalTab.Add(terminalContent);
      tabView.Add(terminalTab);

      // Tab 3: File Opening
      var fileOpeningTab = new Tab("File Opening");
      var fileOpeningContent = new VisualElement();
      fileOpeningContent.style.paddingTop = 10;
      fileOpeningContent.style.paddingBottom = 10;
      fileOpeningContent.style.paddingLeft = 10;
      fileOpeningContent.style.paddingRight = 10;
      CreateFileOpeningSection(fileOpeningContent);
      fileOpeningTab.Add(fileOpeningContent);
      tabView.Add(fileOpeningTab);

      // Tab 4: Maintenance
      var maintenanceTab = new Tab("Maintenance");
      var maintenanceContent = new VisualElement();
      maintenanceContent.style.paddingTop = 10;
      maintenanceContent.style.paddingBottom = 10;
      maintenanceContent.style.paddingLeft = 10;
      maintenanceContent.style.paddingRight = 10;
      CreateMaintenanceSection(maintenanceContent);
      maintenanceTab.Add(maintenanceContent);
      tabView.Add(maintenanceTab);

      rootVisualElement.Add(tabView);
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
      // Deep-copy bindings so we don't mutate config until user clicks Apply
      m_Bindings = NeovimCodeEditor.s_Config.ModifierBindings
        .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args })
        .ToList();

      // ── Two-panel layout ───────────────────────────────────────────────────
      var twoPanel = new VisualElement();
      twoPanel.style.flexDirection = FlexDirection.Row;
      twoPanel.style.flexGrow = 1;
      container.Add(twoPanel);

      // ── LEFT panel: Modifier Bindings ───────────────────────────────────────
      var leftPanel = new VisualElement();
      leftPanel.style.flexGrow = 1;
      leftPanel.style.flexDirection = FlexDirection.Column;
      leftPanel.style.borderRightWidth = 1;
      leftPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
      leftPanel.style.paddingRight = 4;

      var leftTitle = new Label("Modifier Bindings");
      leftTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      leftTitle.style.marginBottom = 4;
      leftTitle.style.marginTop = 4;
      leftTitle.style.marginLeft = 4;
      leftPanel.Add(leftTitle);

      var windowDesc = new Label("Configure how Neovim opens files when clicked in Unity, per modifier key:");
      windowDesc.style.whiteSpace = WhiteSpace.Normal;
      windowDesc.style.marginBottom = 6;
      windowDesc.style.marginLeft = 4;
      windowDesc.style.fontSize = 10;
      leftPanel.Add(windowDesc);

      var scrollView = new ScrollView(ScrollViewMode.Vertical);
      scrollView.style.flexGrow = 1;

      m_BindingRows = new VisualElement();
      m_BindingRows.style.flexDirection = FlexDirection.Column;
      scrollView.Add(m_BindingRows);
      leftPanel.Add(scrollView);

      // Populate binding rows
      RebuildBindingRows();

      // Bottom toolbar: [+] Add binding + [Apply]
      var toolbar = new VisualElement();
      toolbar.style.flexDirection = FlexDirection.Row;
      toolbar.style.justifyContent = Justify.SpaceBetween;
      toolbar.style.marginTop = 6;
      toolbar.style.marginBottom = 4;
      toolbar.style.marginLeft = 4;
      toolbar.style.marginRight = 4;

      var addBtn = new Button(() =>
      {
        m_Bindings.Add(new ModifierBinding { Modifiers = (int)EventModifiers.Shift, Args = NeovimCodeEditor.s_OpenFileArgsTemplates[0].Args });
        RebuildBindingRows();
      })
      { text = "+ Add binding" };

      var applyBtn = new Button(() =>
      {
        NeovimCodeEditor.s_Config.ModifierBindings = m_Bindings
          .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args })
          .ToList();
        NeovimCodeEditor.s_Config.Save();
      })
      { text = "Apply" };

      toolbar.Add(addBtn);
      toolbar.Add(applyBtn);
      leftPanel.Add(toolbar);

      // ── RIGHT panel: Template Info + Jump args ─────────────────────────────
      var rightPanel = new VisualElement();
      rightPanel.style.width = 280;
      rightPanel.style.flexShrink = 0;
      rightPanel.style.flexDirection = FlexDirection.Column;
      rightPanel.style.paddingLeft = 8;
      rightPanel.style.paddingTop = 8;
      rightPanel.style.paddingRight = 4;

      // Template Info section
      var rightTitle = new Label("Template Info");
      rightTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      rightTitle.style.marginBottom = 6;
      rightPanel.Add(rightTitle);

      m_InfoName = new Label();
      m_InfoName.style.unityFontStyleAndWeight = FontStyle.Bold;
      m_InfoName.style.marginBottom = 4;
      m_InfoName.style.whiteSpace = WhiteSpace.Normal;
      rightPanel.Add(m_InfoName);

      m_InfoDesc = new Label();
      m_InfoDesc.style.whiteSpace = WhiteSpace.Normal;
      m_InfoDesc.style.flexWrap = Wrap.Wrap;
      rightPanel.Add(m_InfoDesc);

      SetInfoPanel(null);

      // Separator
      var separator = new VisualElement();
      separator.style.borderTopWidth = 1;
      separator.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
      separator.style.marginTop = 8;
      separator.style.marginBottom = 6;
      rightPanel.Add(separator);

      // Placeholder reference
      var placeholderTitle = new Label("Open-File Placeholders");
      placeholderTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      placeholderTitle.style.marginBottom = 4;
      rightPanel.Add(placeholderTitle);

      var placeholderInfo = new Label(
        "{filePath} — path to the file being opened.\n\n"
        + "{serverSocket} — socket used to communicate with the Neovim server (Unix domain socket on Linux, TCP address on Windows).");
      placeholderInfo.style.whiteSpace = WhiteSpace.Normal;
      placeholderInfo.style.flexWrap = Wrap.Wrap;
      placeholderInfo.style.fontSize = 10;
      rightPanel.Add(placeholderInfo);

      // Jump-to-Cursor Arguments section
      var jumpSeparator = new VisualElement();
      jumpSeparator.style.borderTopWidth = 1;
      jumpSeparator.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
      jumpSeparator.style.marginTop = 12;
      jumpSeparator.style.marginBottom = 6;
      rightPanel.Add(jumpSeparator);

      var jumpTitle = new Label("Jump-to-Cursor Arguments");
      jumpTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      jumpTitle.style.marginBottom = 4;
      rightPanel.Add(jumpTitle);

      var jumpField = new TextField
      {
        label = "Arguments",
        tooltip = "Arguments when jumping to a specific line/column in Neovim.",
        value = NeovimCodeEditor.s_Config.JumpToCursorPositionArgs
      };
      rightPanel.Add(jumpField);

      var jumpHelp = new Label(
        "Placeholders:\n{serverSocket} - Socket for Neovim communication\n{line} - Line number\n{column} - Column number"
      )
      {
        style = { fontSize = 10, whiteSpace = WhiteSpace.Normal, marginTop = 2 }
      };
      rightPanel.Add(jumpHelp);

      var updateJumpBtn = new Button(() =>
      {
        NeovimCodeEditor.s_Config.JumpToCursorPositionArgs = jumpField.value;
        NeovimCodeEditor.s_Config.Save();
      })
      { text = "Update" };
      updateJumpBtn.style.marginTop = 5;
      rightPanel.Add(updateJumpBtn);

      twoPanel.Add(leftPanel);
      twoPanel.Add(rightPanel);
    }

    private void RebuildBindingRows()
    {
      m_BindingRows.Clear();

      for (int i = 0; i < m_Bindings.Count; i++)
      {
        int idx = i; // capture for closure
        var binding = m_Bindings[i];
        bool isDefault = binding.Modifiers == 0;

        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Column;
        row.style.marginBottom = 6;
        row.style.marginLeft = 4;
        row.style.borderBottomWidth = 1;
        row.style.borderBottomColor = new Color(0.25f, 0.25f, 0.25f);
        row.style.paddingBottom = 4;

        // Row header: modifier toggles (skip for default) + delete button
        var headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.alignItems = Align.Center;

        if (isDefault)
        {
          var defaultLabel = new Label("Default (no modifier)");
          defaultLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
          defaultLabel.style.flexGrow = 1;
          headerRow.Add(defaultLabel);
        }
        else
        {
          var modLabel = new Label("Modifiers:");
          modLabel.style.marginRight = 4;
          headerRow.Add(modLabel);

          var shiftToggle = new Toggle("Shift") { value = (binding.Modifiers & (int)EventModifiers.Shift) != 0 };
          var ctrlToggle = new Toggle("Ctrl") { value = (binding.Modifiers & (int)EventModifiers.Control) != 0 };
          var altToggle = new Toggle("Alt") { value = (binding.Modifiers & (int)EventModifiers.Alt) != 0 };

          foreach (var toggle in new[] { shiftToggle, ctrlToggle, altToggle })
          {
            toggle.style.marginRight = 2;
          }

          System.Action updateMods = () =>
          {
            int mods = 0;
            if (shiftToggle.value) mods |= (int)EventModifiers.Shift;
            if (ctrlToggle.value) mods |= (int)EventModifiers.Control;
            if (altToggle.value) mods |= (int)EventModifiers.Alt;
            m_Bindings[idx].Modifiers = mods;
          };

          shiftToggle.RegisterValueChangedCallback(_ => updateMods());
          ctrlToggle.RegisterValueChangedCallback(_ => updateMods());
          altToggle.RegisterValueChangedCallback(_ => updateMods());

          headerRow.Add(shiftToggle);
          headerRow.Add(ctrlToggle);
          headerRow.Add(altToggle);

          // spacer
          var spacer = new VisualElement();
          spacer.style.flexGrow = 1;
          headerRow.Add(spacer);

          var deleteBtn = new Button(() =>
          {
            m_Bindings.RemoveAt(idx);
            RebuildBindingRows();
          })
          { text = "×" };
          deleteBtn.style.color = new Color(1f, 0.4f, 0.4f);
          headerRow.Add(deleteBtn);
        }

        row.Add(headerRow);

        // Template dropdown
        string currentTemplateName = GetTemplateName(binding.Args);
        var templateDd = new DropdownField("Template", s_TemplateNames, s_TemplateNames.IndexOf(currentTemplateName));
        templateDd.style.marginTop = 4;

        // Args text field
        var argsField = new TextField { value = binding.Args };
        argsField.style.marginTop = 2;

        templateDd.RegisterValueChangedCallback(e =>
        {
          if (e.newValue == k_CustomLabel)
          {
            SetInfoPanel(null);
            return;
          }
          var template = NeovimCodeEditor.s_OpenFileArgsTemplates
            .FirstOrDefault(t => t.Name == e.newValue);
          if (template.Name == null) return;
          argsField.SetValueWithoutNotify(template.Args);
          m_Bindings[idx].Args = template.Args;
          SetInfoPanel(template);
        });

        argsField.RegisterValueChangedCallback(e =>
        {
          m_Bindings[idx].Args = e.newValue;
          // if user edited manually, update dropdown to Custom
          if (GetTemplateName(e.newValue) == k_CustomLabel)
            templateDd.SetValueWithoutNotify(k_CustomLabel);
        });

        row.Add(templateDd);
        row.Add(argsField);
        m_BindingRows.Add(row);
      }
    }

    private static string GetTemplateName(string args)
    {
      var match = NeovimCodeEditor.s_OpenFileArgsTemplates
        .FirstOrDefault(t => t.Args == args);
      return match.Name ?? k_CustomLabel;
    }

    private void SetInfoPanel((string Args, string Name, string Desc)? template)
    {
      if (template == null)
      {
        m_InfoName.text = "";
        m_InfoDesc.text = "Select a template to see its description.";
      }
      else
      {
        m_InfoName.text = template.Value.Name;
        m_InfoDesc.text = template.Value.Desc;
      }
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
