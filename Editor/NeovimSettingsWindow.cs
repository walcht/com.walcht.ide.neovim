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
    }

    public void CreateGUI()
    {
      // Create TabView with persistent state
      var tabView = new TabView();
      tabView.viewDataKey = "neovim-settings-tabview";
      tabView.style.flexGrow = 1;

      // Tab 1: General (Behavior + Maintenance)
      var generalTab = new Tab("General");
      var generalContent = new VisualElement();
      generalContent.style.paddingTop = 10;
      generalContent.style.paddingBottom = 10;
      generalContent.style.paddingLeft = 10;
      generalContent.style.paddingRight = 10;
      CreateBehaviorSection(generalContent);
      AddSeparator(generalContent);
      CreateMaintenanceSection(generalContent);
      generalTab.Add(generalContent);
      tabView.Add(generalTab);

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

      rootVisualElement.Add(tabView);
    }

    private void CreateBehaviorSection(VisualElement container)
    {
      var header = new Label("Process Settings")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
      };
      container.Add(header);

      // Two-column layout
      var row = new VisualElement();
      row.style.flexDirection = FlexDirection.Row;
      container.Add(row);

      // LEFT: Settings
      var leftPanel = new VisualElement();
      leftPanel.style.flexGrow = 1;
      leftPanel.style.flexDirection = FlexDirection.Column;
      leftPanel.style.borderRightWidth = 1;
      leftPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
      leftPanel.style.paddingRight = 4;

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
      leftPanel.Add(killToggle);

      leftPanel.Add(new VisualElement { style = { height = 10 } });

      // Process Timeout
      var timeoutLabel = new Label("Process Timeout (milliseconds)")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold }
      };
      leftPanel.Add(timeoutLabel);

      var timeoutField = new IntegerField
      {
        label = "Timeout",
        tooltip = "Process timeout after which the process is killed. Used for open-file, jump-to-cursor, and focus-on-neovim processes.",
        value = NeovimCodeEditor.s_Config.ProcessTimeout
      };
      leftPanel.Add(timeoutField);

      var timeoutError = new Label { style = { color = Color.red, display = DisplayStyle.None } };
      leftPanel.Add(timeoutError);

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
      { text = "Update" };
      updateTimeoutBtn.style.marginTop = 5;
      updateTimeoutBtn.style.marginLeft = 4;
      leftPanel.Add(updateTimeoutBtn);

      // RIGHT: Info panel
      var rightPanel = new VisualElement();
      rightPanel.style.width = 240;
      rightPanel.style.flexShrink = 0;
      rightPanel.style.flexDirection = FlexDirection.Column;
      rightPanel.style.paddingLeft = 8;

      var placeholderTitle = new Label("Timeout");
      placeholderTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      placeholderTitle.style.marginBottom = 4;
      rightPanel.Add(placeholderTitle);

      var placeholderInfo = new Label(
        "Smaller values result in smoother experience at the cost of potential process being killed before completion.\n\n"
        + "Range: 1-1000ms");
      placeholderInfo.style.whiteSpace = WhiteSpace.Normal;
      placeholderInfo.style.flexWrap = Wrap.Wrap;
      placeholderInfo.style.fontSize = 10;
      rightPanel.Add(placeholderInfo);

      row.Add(leftPanel);
      row.Add(rightPanel);
    }

    private void CreateTerminalSection(VisualElement container)
    {
      // Header
      var header = new Label("Terminal Launch Command")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
      };
      container.Add(header);

      // Two-column layout
      var row = new VisualElement();
      row.style.flexDirection = FlexDirection.Row;
      container.Add(row);

      // LEFT: Input fields
      var leftPanel = new VisualElement();
      leftPanel.style.flexGrow = 1;
      leftPanel.style.flexDirection = FlexDirection.Column;
      leftPanel.style.borderRightWidth = 1;
      leftPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
      leftPanel.style.paddingRight = 4;

      // Description
      var desc = new Label("Configure the terminal emulator used when opening a file for the first time.");
      desc.style.whiteSpace = WhiteSpace.Normal;
      desc.style.fontSize = 10;
      desc.style.marginBottom = 6;
      leftPanel.Add(desc);

      // Template dropdown
      var templateNames = NeovimCodeEditor.s_TermLaunchCmds
        .Select((t, _) => t.Item1)
        .Append("Custom")
        .ToList();
      var templateDropdown = new DropdownField("Template", templateNames, 0);
      templateDropdown.SetValueWithoutNotify("Select template...");
      templateDropdown.style.marginTop = 5;
      leftPanel.Add(templateDropdown);

      // Command field
      var cmdField = new TextField
      {
        label = "Command",
        tooltip = "Executable that will be executed when opening a file for the first time. Must be accessible via PATH.",
        value = NeovimCodeEditor.s_Config.TermLaunchCmd ?? ""
      };
      leftPanel.Add(cmdField);

      // Arguments field
      var argsField = new TextField
      {
        label = "Arguments",
        tooltip = "Arguments passed to the executable. Use {app}, {filePath}, {serverSocket}, {instanceId} as placeholders.",
        value = NeovimCodeEditor.s_Config.TermLaunchArgs ?? ""
      };
      leftPanel.Add(argsField);

      // Environment field
      var envField = new TextField
      {
        label = "Environment Variables",
        tooltip = "Space-separated list: ENV_0=VALUE_0 ENV_1=VALUE_1",
        value = NeovimCodeEditor.s_Config.TermLaunchEnv ?? ""
      };
      leftPanel.Add(envField);

      // Status message
      var statusLabel = new Label { style = { color = Color.green, marginTop = 5, marginLeft = 4 } };
      leftPanel.Add(statusLabel);

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
      updateBtn.style.marginLeft = 4;
      leftPanel.Add(updateBtn);

      // Template dropdown callback
      templateDropdown.RegisterValueChangedCallback(e =>
      {
        if (e.newValue == "Custom") return;
        var template = NeovimCodeEditor.s_TermLaunchCmds.First(t => t.Item1 == e.newValue);
        cmdField.value = template.Item1;
        argsField.value = template.Item2;
        envField.value = template.Item3;
      });

      // RIGHT: Info panel
      var rightPanel = new VisualElement();
      rightPanel.style.width = 240;
      rightPanel.style.flexShrink = 0;
      rightPanel.style.flexDirection = FlexDirection.Column;
      rightPanel.style.paddingLeft = 8;

      var placeholderTitle = new Label("Placeholders");
      placeholderTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      placeholderTitle.style.marginBottom = 4;
      rightPanel.Add(placeholderTitle);

      var placeholderInfo = new Label(
        "{app} — Neovim executable path.\n\n"
        + "{filePath} — Path to file being opened.\n\n"
        + "{serverSocket} — Socket for Neovim server communication.\n\n"
        + "{instanceId} — Unity process ID.\n\n"
#if UNITY_EDITOR_WIN
        + "{getProcessPPIDScriptPath} — Path to GetProcessPPID.ps1 for window focusing.\n\n"
#endif
        + "{environment} — Environment variables from the field above.");
      placeholderInfo.style.whiteSpace = WhiteSpace.Normal;
      placeholderInfo.style.flexWrap = Wrap.Wrap;
      placeholderInfo.style.fontSize = 10;
      rightPanel.Add(placeholderInfo);

      row.Add(leftPanel);
      row.Add(rightPanel);
    }

    private void CreateFileOpeningSection(VisualElement container)
    {
      // Deep-copy bindings so we don't mutate config until user clicks Apply
      m_Bindings = NeovimCodeEditor.s_Config.ModifierBindings
        .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args })
        .ToList();

      // ── SECTION 1: File clicking ──────────────────────────────────────────
      var section1 = new VisualElement();
      section1.style.flexDirection = FlexDirection.Column;
      container.Add(section1);

      var section1Title = new Label("File clicking");
      section1Title.style.unityFontStyleAndWeight = FontStyle.Bold;
      section1Title.style.marginBottom = 8;
      section1.Add(section1Title);

      var section1Row = new VisualElement();
      section1Row.style.flexDirection = FlexDirection.Row;
      section1.Add(section1Row);

      // LEFT: Modifier Bindings
      var bindingsPanel = new VisualElement();
      bindingsPanel.style.flexGrow = 1;
      bindingsPanel.style.flexDirection = FlexDirection.Column;
      bindingsPanel.style.borderRightWidth = 1;
      bindingsPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
      bindingsPanel.style.paddingRight = 4;

      var bindingsDesc = new Label("Configure how Neovim opens files when clicked in Unity, per modifier key:");
      bindingsDesc.style.whiteSpace = WhiteSpace.Normal;
      bindingsDesc.style.marginBottom = 6;
      bindingsDesc.style.marginLeft = 4;
      bindingsDesc.style.fontSize = 10;
      bindingsPanel.Add(bindingsDesc);

      var scrollView = new ScrollView(ScrollViewMode.Vertical);
      scrollView.style.flexGrow = 1;
      scrollView.style.minHeight = 120;

      m_BindingRows = new VisualElement();
      m_BindingRows.style.flexDirection = FlexDirection.Column;
      scrollView.Add(m_BindingRows);
      bindingsPanel.Add(scrollView);

      RebuildBindingRows();

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
      bindingsPanel.Add(toolbar);

      // RIGHT: Info
      var infoPanel = new VisualElement();
      infoPanel.style.width = 240;
      infoPanel.style.flexShrink = 0;
      infoPanel.style.flexDirection = FlexDirection.Column;
      infoPanel.style.paddingLeft = 8;

      m_InfoName = new Label();
      m_InfoName.style.unityFontStyleAndWeight = FontStyle.Bold;
      m_InfoName.style.marginBottom = 4;
      m_InfoName.style.whiteSpace = WhiteSpace.Normal;
      infoPanel.Add(m_InfoName);

      m_InfoDesc = new Label();
      m_InfoDesc.style.whiteSpace = WhiteSpace.Normal;
      m_InfoDesc.style.flexWrap = Wrap.Wrap;
      m_InfoDesc.style.fontSize = 10;
      infoPanel.Add(m_InfoDesc);

      SetInfoPanel(null);

      var placeholderTitle = new Label("Placeholders");
      placeholderTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      placeholderTitle.style.marginBottom = 4;
      placeholderTitle.style.marginTop = 12;
      infoPanel.Add(placeholderTitle);

      var placeholderInfo = new Label(
        "{filePath} — path to the file being opened.\n\n"
        + "{serverSocket} — socket used to communicate with the Neovim server (Unix domain socket on Linux, TCP address on Windows).");
      placeholderInfo.style.whiteSpace = WhiteSpace.Normal;
      placeholderInfo.style.flexWrap = Wrap.Wrap;
      placeholderInfo.style.fontSize = 10;
      infoPanel.Add(placeholderInfo);

      section1Row.Add(bindingsPanel);
      section1Row.Add(infoPanel);

      // ── Separator between sections ───────────────────────────────────────
      var sectionSeparator = new VisualElement();
      sectionSeparator.style.borderTopWidth = 1;
      sectionSeparator.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
      sectionSeparator.style.marginTop = 16;
      sectionSeparator.style.marginBottom = 16;
      container.Add(sectionSeparator);

      // ── SECTION 2: Console item clicking ───────────────────────────────────
      var section2 = new VisualElement();
      section2.style.flexDirection = FlexDirection.Column;
      container.Add(section2);

      var section2Title = new Label("Console item clicking");
      section2Title.style.unityFontStyleAndWeight = FontStyle.Bold;
      section2Title.style.marginBottom = 8;
      section2.Add(section2Title);

      var section2Row = new VisualElement();
      section2Row.style.flexDirection = FlexDirection.Row;
      section2.Add(section2Row);

      // LEFT: Jump args field
      var jumpPanel = new VisualElement();
      jumpPanel.style.flexGrow = 1;
      jumpPanel.style.flexDirection = FlexDirection.Column;
      jumpPanel.style.borderRightWidth = 1;
      jumpPanel.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
      jumpPanel.style.paddingRight = 4;

      var jumpDesc = new Label("Used when double-clicking Console errors/warnings to jump to exact line/column in Neovim.");
      jumpDesc.style.whiteSpace = WhiteSpace.Normal;
      jumpDesc.style.fontSize = 10;
      jumpDesc.style.marginLeft = 4;
      jumpDesc.style.marginBottom = 6;
      jumpPanel.Add(jumpDesc);

      var jumpField = new TextField
      {
        label = "Arguments",
        tooltip = "Arguments when jumping to a specific line/column in Neovim.",
        value = NeovimCodeEditor.s_Config.JumpToCursorPositionArgs
      };
      jumpPanel.Add(jumpField);

      var updateJumpBtn = new Button(() =>
      {
        NeovimCodeEditor.s_Config.JumpToCursorPositionArgs = jumpField.value;
        NeovimCodeEditor.s_Config.Save();
      })
      { text = "Update" };
      updateJumpBtn.style.marginTop = 5;
      updateJumpBtn.style.marginLeft = 4;
      jumpPanel.Add(updateJumpBtn);

      // RIGHT: Info (Placeholders)
      var jumpInfoPanel = new VisualElement();
      jumpInfoPanel.style.width = 240;
      jumpInfoPanel.style.flexShrink = 0;
      jumpInfoPanel.style.flexDirection = FlexDirection.Column;
      jumpInfoPanel.style.paddingLeft = 8;

      var jumpPlaceholderTitle = new Label("Placeholders");
      jumpPlaceholderTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      jumpPlaceholderTitle.style.marginBottom = 4;
      jumpInfoPanel.Add(jumpPlaceholderTitle);

      var jumpPlaceholderInfo = new Label(
        "{serverSocket} — socket used to communicate with the Neovim server (Unix domain socket on Linux, TCP address on Windows).\n\n"
        + "{line} — line number to jump to.\n\n"
        + "{column} — column number to jump to.");
      jumpPlaceholderInfo.style.whiteSpace = WhiteSpace.Normal;
      jumpPlaceholderInfo.style.flexWrap = Wrap.Wrap;
      jumpPlaceholderInfo.style.fontSize = 10;
      jumpInfoPanel.Add(jumpPlaceholderInfo);

      section2Row.Add(jumpPanel);
      section2Row.Add(jumpInfoPanel);
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
        m_InfoDesc.text = "You may select a template from each row's dropdown.";
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

    private void AddSeparator(VisualElement container)
    {
      var separator = new VisualElement();
      separator.style.borderTopWidth = 1;
      separator.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
      separator.style.marginTop = 16;
      separator.style.marginBottom = 16;
      container.Add(separator);
    }
  }
}
