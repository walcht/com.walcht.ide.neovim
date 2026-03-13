#pragma warning disable IDE0130, IDE0031
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Unity.CodeEditor;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace Neovim.Editor
{
  public class NeovimSettingsWindow : EditorWindow
  {
    // toolbar
    private Button m_ApplyBtn = null;
    private Button m_AddBindingModifierBtn = null;
    private Button m_ResetBtn = null;
    private Button m_RegenerateProjectFilesBtn = null;
#if UNITY_EDITOR_WIN
    private TextField m_ProcessPIDPlaceholderTf = null;
#endif

    // right panel stuff
    private TextField m_AppPlaceholderTf;
    private TextField m_ServerSocketTf;
    private TextField m_InstanceIdTf;
    private Label m_InfoName;
    private Label m_InfoDesc;

    // visual tree (i.e., uxml) assets
    private static readonly VisualTreeAsset s_MainWindowVT;
    private static readonly VisualTreeAsset s_DefaultModifierBindingVT;
    private static readonly VisualTreeAsset s_ModifierBindingVT;
    private static readonly VisualTreeAsset s_AnalyzerEntryVT;

    private static float s_X = Mathf.FloorToInt(Screen.width * 0.5f - Screen.width * 0.25f);
    private static float s_Y = Mathf.FloorToInt(Screen.height * 0.5f - Screen.height * 0.25f);
    private static float s_Width = Screen.width * 0.5f;
    private static float s_Height = Screen.height * 0.5f;

    ////////////////////////////////////////////////////////////////////////////
    // project generation flags
    ////////////////////////////////////////////////////////////////////////////
    private Toggle m_EmbeddedPackagesTg;
    private Toggle m_LocalPackagesTg;
    private Toggle m_RegistryPackagesTg;
    private Toggle m_GitPackagesTg;
    private Toggle m_BuiltinPackagesTg;
    private Toggle m_LocalTarballPackagesTg;
    private Toggle m_UnknownSourcePackagesTg;

    ////////////////////////////////////////////////////////////////////////////
    // nvim executable path
    ////////////////////////////////////////////////////////////////////////////
    private TextField m_NvimExecutablePathTf = null;

    ////////////////////////////////////////////////////////////////////////////
    // Jump-to-cursor position args
    ////////////////////////////////////////////////////////////////////////////
    private TextField m_JumpToCursorPosArgsTf;
    private static List<string> s_JumpToCursorPosTemplateNames;
    private static readonly string k_CustomLabel = "Custom";

    ////////////////////////////////////////////////////////////////////////////
    // Open-file-request args
    ////////////////////////////////////////////////////////////////////////////
    private static readonly Dictionary<string, int> s_OpenFileModifiers = new()
    {
      ["SHIFT"] = (int)EventModifiers.Shift,
      ["CTRL"] = (int)EventModifiers.Control,
      ["ALT"] = (int)EventModifiers.Alt,
      ["SHIFT+CTRL"] = (int)EventModifiers.Shift | (int)EventModifiers.Control,
      ["SHIFT+ALT"] = (int)EventModifiers.Shift | (int)EventModifiers.Alt,
      ["CTRL+ALT"] = (int)EventModifiers.Control | (int)EventModifiers.Alt,
      ["SHIFT+CTRL+ALT"] = (int)EventModifiers.Shift | (int)EventModifiers.Control | (int)EventModifiers.Alt
    };
    private List<ModifierBinding> m_ModifierBindings;
    private VisualElement m_ModifierBindingRows;
    private static List<string> s_OpenFileTemplateNames;

    ////////////////////////////////////////////////////////////////////////////
    // Terminal launch cmd args
    ////////////////////////////////////////////////////////////////////////////
    private TextField m_TermLaunchCmdTf;
    private TextField m_TermLaunchArgsTf;
    private TextField m_TermLaunchEnvTf;
    private static List<string> s_TermLaunchCmdTemplateNames;

    ////////////////////////////////////////////////////////////////////////////
    // Process timeout
    ////////////////////////////////////////////////////////////////////////////
    private IntegerField m_ProcessTimeoutIf = null;

    ////////////////////////////////////////////////////////////////////////////
    // Analyzers
    ////////////////////////////////////////////////////////////////////////////
    private VisualElement m_AnalyzerRows;
    private VisualElement m_AnalyzerRowsParent;


    // TODO: persistent window position
    static NeovimSettingsWindow()
    {
      s_MainWindowVT = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.walcht.ide.neovim/Editor/settings_window.uxml");
      s_DefaultModifierBindingVT = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.walcht.ide.neovim/Editor/default_modifier_binding.uxml");
      s_ModifierBindingVT = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.walcht.ide.neovim/Editor/modifier_binding.uxml");
      s_AnalyzerEntryVT = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.walcht.ide.neovim/Editor/analyzer_entry.uxml");
    }


    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    // TODO: don't show if Neovim is not chosen in External Editor Tools
    [MenuItem("Neovim/Settings")]
    public static void ShowWindow()
    {
      var window = GetWindow<NeovimSettingsWindow>(true, "Neovim Settings");
      // keep this shit in this order - if you set position THEN minSize, position will be reset ...
      window.minSize = new Vector2(650, 300);
      window.position = new Rect(s_X, s_Y, s_Width, s_Height);
      window.saveChangesMessage = "This window has unsaved changes. Would you like to save?";
      window.ShowModalUtility();

      // save window position so that you do not have to resize it each time
      s_X = window.position.x;
      s_Y = window.position.y;
      s_Width = window.position.width;
      s_Height = window.position.height;
    }


    public override void SaveChanges()
    {
      Save();
      base.SaveChanges();
    }


    public override void DiscardChanges()
    {
      base.DiscardChanges();
    }


    private void SetDirty(bool val)
    {
      hasUnsavedChanges = val;
      if (m_ApplyBtn != null)
        m_ApplyBtn.SetEnabled(val);
    }


    // CreateGUI is called when the EditorWindow's rootVisualElement is ready to be populated.
    private void CreateGUI()
    {
      s_OpenFileTemplateNames = NeovimCodeEditor.s_OpenFileArgsTemplates
        .Select(t => t.Name)
        .Append(k_CustomLabel)
        .ToList();

      s_JumpToCursorPosTemplateNames = NeovimCodeEditor.s_JumpToCursorPositionArgsTemplates
        .Select(t => t.Name)
        .Append(k_CustomLabel)
        .ToList();

      s_TermLaunchCmdTemplateNames = NeovimCodeEditor.s_TermLaunchCmds
        .Select(cmds => cmds.Item1)
        .Append(k_CustomLabel)
        .ToList();

      var root = rootVisualElement;

      VisualElement mainPanel = s_MainWindowVT.Instantiate();
      mainPanel.style.flexGrow = 1;  // keep this because Unity UIToolkit still sucks...

      // toolbar buttons
      m_ApplyBtn = mainPanel.Q<Button>("apply");
      m_ResetBtn = mainPanel.Q<Button>("reset");
      m_AddBindingModifierBtn = mainPanel.Q<Button>("add-binding");
      m_RegenerateProjectFilesBtn = mainPanel.Q<Button>("regenerate-project-files-btn");

      m_ApplyBtn.clicked += OnApplyClick;
      m_ResetBtn.clicked += OnResetClick;
      m_AddBindingModifierBtn.clicked += OnAddModifierBindingClick;
      m_RegenerateProjectFilesBtn.clicked += OnProjectRegenerationClick;

      // csproj generation settings
      {
        m_EmbeddedPackagesTg = mainPanel.Q<Toggle>("embedded-packages-tg");
        m_EmbeddedPackagesTg.SetValueWithoutNotify(NeovimCodeEditor.CsprojFlags.HasFlag(ProjectGenerationFlag.Embedded));
        m_EmbeddedPackagesTg.RegisterValueChangedCallback(OnProjectGenerationFlag);

        m_LocalPackagesTg = mainPanel.Q<Toggle>("local-packages-tg");
        m_LocalPackagesTg.SetValueWithoutNotify(NeovimCodeEditor.CsprojFlags.HasFlag(ProjectGenerationFlag.Local));
        m_LocalPackagesTg.RegisterValueChangedCallback(OnProjectGenerationFlag);

        m_RegistryPackagesTg = mainPanel.Q<Toggle>("registry-packages-tg");
        m_RegistryPackagesTg.SetValueWithoutNotify(NeovimCodeEditor.CsprojFlags.HasFlag(ProjectGenerationFlag.Registry));
        m_RegistryPackagesTg.RegisterValueChangedCallback(OnProjectGenerationFlag);

        m_GitPackagesTg = mainPanel.Q<Toggle>("git-packages-tg");
        m_GitPackagesTg.SetValueWithoutNotify(NeovimCodeEditor.CsprojFlags.HasFlag(ProjectGenerationFlag.Git));
        m_GitPackagesTg.RegisterValueChangedCallback(OnProjectGenerationFlag);

        m_BuiltinPackagesTg = mainPanel.Q<Toggle>("built-in-packages-tg");
        m_BuiltinPackagesTg.SetValueWithoutNotify(NeovimCodeEditor.CsprojFlags.HasFlag(ProjectGenerationFlag.BuiltIn));
        m_BuiltinPackagesTg.RegisterValueChangedCallback(OnProjectGenerationFlag);

        m_LocalTarballPackagesTg = mainPanel.Q<Toggle>("local-tarball-packages-tg");
        m_LocalTarballPackagesTg.SetValueWithoutNotify(NeovimCodeEditor.CsprojFlags.HasFlag(ProjectGenerationFlag.LocalTarBall));
        m_LocalTarballPackagesTg.RegisterValueChangedCallback(OnProjectGenerationFlag);

        m_UnknownSourcePackagesTg = mainPanel.Q<Toggle>("unknown-source-packages-tg");
        m_UnknownSourcePackagesTg.SetValueWithoutNotify(NeovimCodeEditor.CsprojFlags.HasFlag(ProjectGenerationFlag.Unknown));
        m_UnknownSourcePackagesTg.RegisterValueChangedCallback(OnProjectGenerationFlag);
      }

      // nvim executable path args
      {
        m_NvimExecutablePathTf = mainPanel.Q<TextField>("nvim-exec-path-tf");
        m_NvimExecutablePathTf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.NvimExecutablePath);
        m_NvimExecutablePathTf.RegisterValueChangedCallback(e =>
        {
          if (e.newValue == NeovimCodeEditor.s_Config.NvimExecutablePath)
            return;
          SetDirty(true);
        });
      }

      // terminal launch cmd args
      {
        var termLaunchDf = mainPanel.Q<DropdownField>("terminal-launch-templates-dd");
        m_TermLaunchCmdTf = mainPanel.Q<TextField>("terminal-launch-cmd-tf");
        m_TermLaunchArgsTf = mainPanel.Q<TextField>("terminal-launch-args-tf");
        m_TermLaunchEnvTf = mainPanel.Q<TextField>("terminal-launch-env-tf");
        termLaunchDf.choices = s_TermLaunchCmdTemplateNames;
        termLaunchDf.SetValueWithoutNotify("select template");
        m_TermLaunchCmdTf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.TermLaunchCmd);
        m_TermLaunchArgsTf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.TermLaunchArgs);
        m_TermLaunchEnvTf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.TermLaunchEnv);

        termLaunchDf.RegisterValueChangedCallback(e =>
        {
          if (e.newValue == k_CustomLabel)
          {
            return;
          }
          var template = NeovimCodeEditor.s_TermLaunchCmds
            .FirstOrDefault(t => t.Item1 == e.newValue);
          if (template.Item1 == null) return;
          m_TermLaunchCmdTf.value = template.Item1;
          m_TermLaunchArgsTf.value = template.Item2;
        });

        m_TermLaunchCmdTf.RegisterValueChangedCallback(e =>
        {
          var templateName = NeovimCodeEditor.s_TermLaunchCmds
            .FirstOrDefault(t => (t.Item1 == e.newValue) && (t.Item2 == m_TermLaunchArgsTf.value)).Item1 ?? k_CustomLabel;
          termLaunchDf.SetValueWithoutNotify(templateName);
          if (e.newValue == NeovimCodeEditor.s_Config.TermLaunchCmd)
            return;
          SetDirty(true);
        });

        m_TermLaunchArgsTf.RegisterValueChangedCallback(e =>
        {
          var templateName = NeovimCodeEditor.s_TermLaunchCmds
            .FirstOrDefault(t => (t.Item2 == e.newValue) && (t.Item1 == m_TermLaunchCmdTf.value)).Item1 ?? k_CustomLabel;
          termLaunchDf.SetValueWithoutNotify(templateName);
          if (e.newValue == NeovimCodeEditor.s_Config.TermLaunchArgs)
            return;
          SetDirty(true);
        });

        m_TermLaunchEnvTf.RegisterValueChangedCallback(e =>
        {
          if (e.newValue == NeovimCodeEditor.s_Config.TermLaunchEnv)
            return;
          SetDirty(true);
        });
      }

      // jumo-to-cursor-position
      {
        string currArgs = NeovimCodeEditor.s_Config.JumpToCursorPositionArgs;
        string currentTemplateName = GetJumpToCursorPosTemplateName(currArgs);
        var templatesDd = mainPanel.Q<DropdownField>("jump-to-cursor-pos-templates");
        templatesDd.choices = s_JumpToCursorPosTemplateNames;
        templatesDd.SetValueWithoutNotify(currentTemplateName);
        m_JumpToCursorPosArgsTf = mainPanel.Q<TextField>("jump-to-cursor-pos-args-tf");
        m_JumpToCursorPosArgsTf.SetValueWithoutNotify(currArgs);

        m_JumpToCursorPosArgsTf.RegisterValueChangedCallback(e =>
        {
          string templateName = GetJumpToCursorPosTemplateName(e.newValue);
          templatesDd.SetValueWithoutNotify(templateName);
          if (e.newValue == NeovimCodeEditor.s_Config.JumpToCursorPositionArgs)
            return;
          SetDirty(true);
        });

        templatesDd.RegisterValueChangedCallback(e =>
        {
          if (e.newValue == k_CustomLabel)
          {
            SetInfoPanel(null);
            return;
          }
          var template = NeovimCodeEditor.s_JumpToCursorPositionArgsTemplates
            .FirstOrDefault(t => t.Name == e.newValue);
          if (template.Name == null) return;
          m_JumpToCursorPosArgsTf.value = template.Args;
          SetInfoPanel(template);
        });
      }

      // open-file request args
      {
        // deep-copy bindings so we don't mutate config until user clicks Update
        m_ModifierBindings = NeovimCodeEditor.s_Config.ModifierBindings
          .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args, Representation = b.Representation })
          .ToList();
        m_ModifierBindingRows = mainPanel.Q<VisualElement>("modifier-binding-rows");
        RebuildModifierBindingRows();
      }

      // process timeout arg
      {
        m_ProcessTimeoutIf = mainPanel.Q<IntegerField>("process-timeout-if");
        m_ProcessTimeoutIf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.ProcessTimeout);
        m_ProcessTimeoutIf.RegisterValueChangedCallback(e =>
        {
          if (e.newValue == NeovimCodeEditor.s_Config.ProcessTimeout)
            return;
          SetDirty(true);
        });
      }

      // analyzers
      {
        m_AnalyzerRows = mainPanel.Q<VisualElement>("analyzer-rows");
        m_AnalyzerRowsParent = mainPanel.Q<VisualElement>("analyzer-rows-parent");
        var analyzerPathTf = mainPanel.Q<TextField>("analyzer-path-tf");
        var browseBtn = mainPanel.Q<Button>("browse-analyzer-btn");
        var addBtn = mainPanel.Q<Button>("add-analyzer-btn");

        addBtn.SetEnabled(false);

        // save color
        var defaultColor = analyzerPathTf.style.color;
        analyzerPathTf.RegisterValueChangedCallback(e =>
        {
          if (File.Exists(e.newValue) && Path.GetExtension(e.newValue) == ".dll")
          {
            addBtn.SetEnabled(true);
            return;
          }
          addBtn.SetEnabled(false);
        });

        browseBtn.clicked += () =>
        {
          string p = EditorUtility.OpenFilePanel("Select analyzer to add (.dll)", "", "dll");
          if (NeovimCodeEditor.TryAddAnalyzer(p))
          {
            RebuildAnalyzerRows();
            return;
          }
        };

        addBtn.clicked += () =>
        {
          if (NeovimCodeEditor.TryAddAnalyzer(analyzerPathTf.value))
          {
            RebuildAnalyzerRows();
            return;
          }
        };

        RebuildAnalyzerRows();
      }

      // info panel (right panel)
      {
        m_InfoName = mainPanel.Q<Label>("curr-template-name");
        m_InfoDesc = mainPanel.Q<Label>("curr-template-desc");
        m_AppPlaceholderTf = mainPanel.Q<TextField>("app-placeholder-tf");
        m_ServerSocketTf = mainPanel.Q<TextField>("serversocket-placeholder-tf");
        m_InstanceIdTf = mainPanel.Q<TextField>("instanceid-placeholder-tf");
#if UNITY_EDITOR_WIN
        m_ProcessPIDPlaceholderTf = mainPanel.Q<TextField>("processpid-placeholder-tf");
        m_ProcessPIDPlaceholderTf.SetValueWithoutNotify(NeovimCodeEditor.s_GetProcessPPIDPath);
#else
        mainPanel.Q<VisualElement>("processpid-placeholder").RemoveFromHierarchy();
#endif
        m_AppPlaceholderTf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.NvimExecutablePath);
        m_ServerSocketTf.SetValueWithoutNotify(NeovimCodeEditor.ServerSocket);
        m_InstanceIdTf.SetValueWithoutNotify(NeovimCodeEditor.s_InstanceId);
      }

      root.Add(mainPanel);
      SetDirty(false);
    }

    private void Save()
    {
      // update ProjectGenerationFlags
      ProjectGenerationFlag flags = ProjectGenerationFlag.None
        | (m_EmbeddedPackagesTg.value ? ProjectGenerationFlag.Embedded : 0)
        | (m_RegistryPackagesTg.value ? ProjectGenerationFlag.Registry : 0)
        | (m_GitPackagesTg.value ? ProjectGenerationFlag.Git : 0)
        | (m_BuiltinPackagesTg.value ? ProjectGenerationFlag.BuiltIn : 0)
        | (m_LocalPackagesTg.value ? ProjectGenerationFlag.Local : 0)
        | (m_LocalTarballPackagesTg.value ? ProjectGenerationFlag.LocalTarBall : 0)
        | (m_UnknownSourcePackagesTg.value ? ProjectGenerationFlag.Unknown : 0);
      // this potentially causes regeneration of csproj
      NeovimCodeEditor.CsprojFlags = flags;

      // update nvim executable path
      m_NvimExecutablePathTf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.NvimExecutablePath = m_NvimExecutablePathTf.value);
      m_AppPlaceholderTf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.NvimExecutablePath);

      // update terminal launch cmd shit
      if (!NeovimCodeEditor.TryChangeTermLaunchCmd(m_TermLaunchCmdTf.value, m_TermLaunchArgsTf.value, m_TermLaunchEnvTf.value))
      {
        // TODO: show popup and change border to red
      }

      // update open-file request modifier bindings args
      NeovimCodeEditor.s_Config.ModifierBindings = m_ModifierBindings
        .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args, Representation = b.Representation })
        .ToList();

      // update jumo-to-cursor-position args
      m_JumpToCursorPosArgsTf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.JumpToCursorPositionArgs = m_JumpToCursorPosArgsTf.value);

      // update process timeout
      m_ProcessTimeoutIf.SetValueWithoutNotify(NeovimCodeEditor.s_Config.ProcessTimeout = m_ProcessTimeoutIf.value);

      // serialize the config shit
      NeovimCodeEditor.s_Config.Save();

      SetDirty(false);
    }


    private void OnApplyClick()
    {
      if (!m_ApplyBtn.enabledSelf) return;
      Save();
    }


    private void OnProjectRegenerationClick() => CodeEditor.Editor.CurrentCodeEditor.SyncAll();


    private void OnProjectGenerationFlag(ChangeEvent<bool> _) => SetDirty(true);


    private void OnAddModifierBindingClick()
    {
      // check if button is already disabled
      if (!m_AddBindingModifierBtn.enabledSelf) return;
      int nextAvailableModifier = GetNextAvailableModifier(out string representation);
      if (nextAvailableModifier == -1)
      {
        return;
      }
      m_ModifierBindings.Add(new ModifierBinding
      {
        Modifiers = nextAvailableModifier,
        Args = NeovimCodeEditor.s_OpenFileArgsTemplates[0].Args,
        Representation = representation
      });
      SetDirty(true);
      RebuildModifierBindingRows();
    }


    private void OnResetClick()
    {
      NeovimCodeEditor.ResetConfig();
      SetDirty(false);
      Close();
      ShowWindow();
    }


    private static string GetJumpToCursorPosTemplateName(string args)
    {
      var (Args, Name, Desc) = NeovimCodeEditor.s_JumpToCursorPositionArgsTemplates
        .FirstOrDefault(t => t.Args == args);
      return Name ?? k_CustomLabel;
    }


    private void SetInfoPanel((string Args, string Name, string Desc)? template)
    {
      if (template == null)
      {
        m_InfoName.text = "No template is currently selected";
        m_InfoDesc.text = "Select a template to see its description.";
      }
      else
      {
        m_InfoName.text = template.Value.Name;
        m_InfoDesc.text = template.Value.Desc;
      }
    }


    private void RebuildAnalyzerRows()
    {
      m_AnalyzerRows.Clear();
      if (!NeovimCodeEditor.s_Config.Analyzers.Any())
      {
        m_AnalyzerRowsParent.visible = false;
        return;
      }

      m_AnalyzerRowsParent.visible = true;
      // show currently used custom analyzers
      for (int i = NeovimCodeEditor.s_Config.Analyzers.Count - 1; i >= 0; --i)
      {
        int j = i;
        VisualElement row = s_AnalyzerEntryVT.Instantiate();
        var analyzerNameLabel = row.Q<Label>("analyzer-name");
        var analyzerPathLabel = row.Q<Label>("analyzer-path");
        var deleteBtn = row.Q<Button>("delete-btn");

        analyzerNameLabel.text = $"{Path.GetFileNameWithoutExtension(NeovimCodeEditor.s_Config.Analyzers[j])}:";
        analyzerPathLabel.text = NeovimCodeEditor.s_Config.Analyzers[j];

        deleteBtn.clicked += () =>
        {
          NeovimCodeEditor.DelAnalyzerAt(j);
          RebuildAnalyzerRows();
        };

        m_AnalyzerRows.Add(row);
      }
    }


    private void RebuildModifierBindingRows()
    {
      m_ModifierBindingRows.Clear();
      for (int i = 0; i < m_ModifierBindings.Count; i++)
      {
        int idx = i; // capture for closure
        var binding = m_ModifierBindings[i];
        bool isDefault = binding.Modifiers == 0;
        VisualElement row;

        if (isDefault)
        {
          row = s_DefaultModifierBindingVT.Instantiate();
        }
        else
        {
          row = s_ModifierBindingVT.Instantiate();

          var modifierDd = row.Q<DropdownField>("modifiers-dd");
          var deleteBtn = row.Q<Button>("delete-btn");

          var _modifiers = s_OpenFileModifiers.Keys.ToList();
          modifierDd.choices = _modifiers;
          modifierDd.SetValueWithoutNotify(binding.Representation);

          modifierDd.RegisterValueChangedCallback(e =>
          {
            // check if current modifier is in use
            int modifier = s_OpenFileModifiers[e.newValue];
            if (IsModifierInUse(modifier))
            {
              modifierDd.SetValueWithoutNotify(e.previousValue);
              return;
            }
            m_ModifierBindings[idx].Modifiers = modifier;
            m_ModifierBindings[idx].Representation = e.newValue;
            SetDirty(true);
          });

          deleteBtn.clicked += () =>
          {
            m_ModifierBindings.RemoveAt(idx);
            RebuildModifierBindingRows();
          };
        }

        // template dropdown
        string currentTemplateName = GetJumpToCursorPosTemplateName(binding.Args);
        var templateDd = row.Q<DropdownField>("templates-dd");
        templateDd.choices = s_OpenFileTemplateNames;
        templateDd.SetValueWithoutNotify(currentTemplateName);

        // args text field
        var argsField = row.Q<TextField>("args-tf");
        argsField.SetValueWithoutNotify(binding.Args);

        // update the info pannel on selection
        templateDd.RegisterCallback<FocusEvent>(_ =>
        {
          var template = NeovimCodeEditor.s_OpenFileArgsTemplates
                      .FirstOrDefault(t => t.Name == templateDd.value);
          if (template.Name == null) return;
          SetInfoPanel(template);
        });

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
          argsField.value = template.Args;
          m_ModifierBindings[idx].Args = template.Args;
          SetInfoPanel(template);
        });

        argsField.RegisterValueChangedCallback(e =>
        {
          m_ModifierBindings[idx].Args = e.newValue;
          // if user edited manually, update dropdown to Custom
          if (GetJumpToCursorPosTemplateName(e.newValue) == k_CustomLabel)
            templateDd.SetValueWithoutNotify(k_CustomLabel);
          SetDirty(true);
        });

        m_ModifierBindingRows.Add(row);

      } // END FOR

      // this means all modifier combinations are used
      m_AddBindingModifierBtn.SetEnabled(m_ModifierBindings.Count < (s_OpenFileModifiers.Count + 1));
    }

    private bool IsModifierInUse(int modifier)
    {
      for (int i = 0; i < m_ModifierBindings.Count; ++i)
      {
        if (m_ModifierBindings[i].Modifiers == modifier)
        {
          return true;
        }
      }
      return false;
    }

    private int GetNextAvailableModifier(out string representation)
    {
      representation = null;
      if (m_ModifierBindings.Count == (s_OpenFileModifiers.Count + 1))
        return -1;
      foreach (var kv in s_OpenFileModifiers)
      {
        if (!IsModifierInUse(kv.Value))
        {
          representation = kv.Key;
          return kv.Value;
        }
      }
      return -1;
    }

  }
}
