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
      container.style.padding = 10;

      // Header
      var header = new Label("Terminal Launch Command");
      header.style.unityFontStyleAndWeight = FontStyle.Bold;
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

    private void CreateFileOpeningTab(VisualElement container)
    {
      container.style.padding = 10;

      // Two-column layout: bindings left, templates right
      var twoColumn = new VisualElement();
      twoColumn.style.flexDirection = FlexDirection.Row;
      twoColumn.style.flexGrow = 1;
      container.Add(twoColumn);

      // LEFT COLUMN: Modifier Bindings
      var leftColumn = new VisualElement
      {
        style =
        {
          flexGrow = 1,
          flexDirection = FlexDirection.Column,
          paddingRight = 10,
          borderRightWidth = 1,
          borderRightColor = new Color(0.3f, 0.3f, 0.3f)
        }
      };
      twoColumn.Add(leftColumn);

      var bindingsTitle = new Label("Modifier Bindings")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 }
      };
      leftColumn.Add(bindingsTitle);

      var bindingsHelp = new Label("Configure how Neovim opens files when clicked in Unity, based on modifier keys:")
      {
        style = { whiteSpace = WhiteSpace.Normal, fontSize = 11, marginBottom = 10 }
      };
      leftColumn.Add(bindingsHelp);

      // ScrollView for binding rows
      var scrollView = new ScrollView(ScrollViewMode.Vertical)
      {
        style = { flexGrow = 1 }
      };
      leftColumn.Add(scrollView);

      m_BindingRows = new VisualElement
      {
        style = { flexDirection = FlexDirection.Column }
      };
      scrollView.Add(m_BindingRows);

      // Initialize bindings from config
      m_Bindings = NeovimCodeEditor.s_Config.ModifierBindings
        .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args })
        .ToList();

      RebuildBindingRows();

      // Toolbar: Add button + Apply button
      var toolbar = new VisualElement
      {
        style = { flexDirection = FlexDirection.Row, marginTop = 10, justifyContent = Justify.SpaceBetween }
      };
      leftColumn.Add(toolbar);

      var addBtn = new Button(() =>
      {
        m_Bindings.Add(new ModifierBinding
        {
          Modifiers = (int)EventModifiers.Shift,
          Args = NeovimCodeEditor.s_OpenFileArgsTemplates[0].Args
        });
        RebuildBindingRows();
      })
      { text = "+ Add Binding" };
      toolbar.Add(addBtn);

      var applyBtn = new Button(() =>
      {
        NeovimCodeEditor.s_Config.ModifierBindings = m_Bindings
          .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args })
          .ToList();
        NeovimCodeEditor.s_Config.Save();
      })
      { text = "Apply" };
      toolbar.Add(applyBtn);

      // RIGHT COLUMN: Template info + Jump args
      var rightColumn = new VisualElement
      {
        style = { width = 220, flexShrink = 0, paddingLeft = 10, flexDirection = FlexDirection.Column }
      };
      twoColumn.Add(rightColumn);

      // Template Info section
      var templateTitle = new Label("Template Info")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 }
      };
      rightColumn.Add(templateTitle);

      m_BindingInfoName = new Label { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 3, whiteSpace = WhiteSpace.Normal } };
      rightColumn.Add(m_BindingInfoName);

      m_BindingInfoDesc = new Label { style = { whiteSpace = WhiteSpace.Normal, flexWrap = Wrap.Wrap } };
      rightColumn.Add(m_BindingInfoDesc);

      SetInfoPanel(null);

      rightColumn.Add(new VisualElement { style = { height = 15 } });

      // Jump-to-Cursor section
      var jumpTitle = new Label("Jump-to-Cursor Arguments")
      {
        style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 }
      };
      rightColumn.Add(jumpTitle);

      var jumpField = new TextField
      {
        label = "Arguments",
        tooltip = "Arguments when jumping to a specific line/column in Neovim.",
        value = NeovimCodeEditor.s_Config.JumpToCursorPositionArgs
      };
      rightColumn.Add(jumpField);

      var jumpTemplates = NeovimCodeEditor.s_JumpToCursorPositionArgsTemplates.ToList();
      var jumpDropdown = new DropdownField("Template", jumpTemplates, 0);
      jumpDropdown.SetValueWithoutNotify("Select template...");
      jumpDropdown.RegisterValueChangedCallback(e =>
      {
        jumpField.value = e.newValue;
      });
      rightColumn.Add(jumpDropdown);

      var jumpHelp = new Label(
        "{serverSocket} - Socket for Neovim communication\n{line} - Line number to jump to\n{column} - Column number")
      {
        style = { fontSize = 10, whiteSpace = WhiteSpace.Normal, marginTop = 5 }
      };
      rightColumn.Add(jumpHelp);

      var jumpApplyBtn = new Button(() =>
      {
        NeovimCodeEditor.s_Config.JumpToCursorPositionArgs = jumpField.value;
        NeovimCodeEditor.s_Config.Save();
      })
      { text = "Update" };
      rightColumn.Add(jumpApplyBtn);
    }

    private void CreateMaintenanceTab(VisualElement container)
    {
      container.style.padding = 10;

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

    private void RebuildBindingRows()
    {
      m_BindingRows.Clear();

      for (int i = 0; i < m_Bindings.Count; i++)
      {
        int idx = i;
        var binding = m_Bindings[i];
        bool isDefault = binding.Modifiers == 0;

        var row = new VisualElement
        {
          style =
          {
            flexDirection = FlexDirection.Column,
            marginBottom = 8,
            borderBottomWidth = 1,
            borderBottomColor = new Color(0.25f, 0.25f, 0.25f),
            paddingBottom = 5
          }
        };

        // Header row
        var header = new VisualElement
        {
          style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
        };
        row.Add(header);

        if (isDefault)
        {
          header.Add(new Label("Default (no modifier)")
          {
            style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 }
          });
        }
        else
        {
          header.Add(new Label("Modifiers:") { style = { marginRight = 5 } });

          var shiftToggle = new Toggle("S") { value = (binding.Modifiers & (int)EventModifiers.Shift) != 0 };
          var ctrlToggle = new Toggle("C") { value = (binding.Modifiers & (int)EventModifiers.Control) != 0 };
          var altToggle = new Toggle("A") { value = (binding.Modifiers & (int)EventModifiers.Alt) != 0 };

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

          foreach (var t in new[] { shiftToggle, ctrlToggle, altToggle })
          {
            t.style.marginRight = 2;
            header.Add(t);
          }

          var spacer = new VisualElement { style = { flexGrow = 1 } };
          header.Add(spacer);

          var deleteBtn = new Button(() =>
          {
            m_Bindings.RemoveAt(idx);
            RebuildBindingRows();
          })
          { text = "×", style = { color = new Color(1f, 0.4f, 0.4f) } };
          header.Add(deleteBtn);
        }

        // Template dropdown
        string currentName = GetTemplateName(binding.Args);
        var templateDd = new DropdownField(s_TemplateNames, s_TemplateNames.IndexOf(currentName));
        row.Add(templateDd);

        // Args field
        var argsField = new TextField { value = binding.Args };
        row.Add(argsField);

        templateDd.RegisterValueChangedCallback(e =>
        {
          if (e.newValue == k_CustomLabel)
          {
            SetInfoPanel(null);
            return;
          }
          var template = NeovimCodeEditor.s_OpenFileArgsTemplates.FirstOrDefault(t => t.Name == e.newValue);
          if (template.Name == null) return;
          argsField.SetValueWithoutNotify(template.Args);
          m_Bindings[idx].Args = template.Args;
          SetInfoPanel(template);
        });

        argsField.RegisterValueChangedCallback(e =>
        {
          m_Bindings[idx].Args = e.newValue;
          if (GetTemplateName(e.newValue) == k_CustomLabel)
            templateDd.SetValueWithoutNotify(k_CustomLabel);
        });

        m_BindingRows.Add(row);
      }
    }

    private static string GetTemplateName(string args)
    {
      var match = NeovimCodeEditor.s_OpenFileArgsTemplates.FirstOrDefault(t => t.Args == args);
      return match.Name ?? k_CustomLabel;
    }

    private void SetInfoPanel((string Args, string Name, string Desc)? template)
    {
      if (template == null)
      {
        m_BindingInfoName.text = "";
        m_BindingInfoDesc.text = "Select a template to see its description.";
      }
      else
      {
        m_BindingInfoName.text = template.Value.Name;
        m_BindingInfoDesc.text = template.Value.Desc;
      }
    }
  }
}
