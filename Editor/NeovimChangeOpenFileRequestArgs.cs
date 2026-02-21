#pragma warning disable IDE0130
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace Neovim.Editor
{
  public class NeovimChangeOpenFileRequestArgs : EditorWindow
  {
    [MenuItem("Neovim/Change Open-File Request Args")]
    static void Init()
    {
      var window = GetWindow<NeovimChangeOpenFileRequestArgs>(true, "Change Open-File Request Args");
      window.position = new Rect(Screen.width / 2, Screen.height / 2, 750, 350);
      window.minSize = new Vector2(600, 300);
      window.ShowModalUtility();
    }

    // working copy of bindings — edited in-place, saved on "Update"
    private List<ModifierBinding> m_Bindings;

    // right-panel elements
    private Label m_InfoName;
    private Label m_InfoDesc;

    // left-panel binding rows container
    private VisualElement m_BindingRows;

    private static readonly List<string> s_TemplateNames;
    private static readonly string k_CustomLabel = "Custom";

    static NeovimChangeOpenFileRequestArgs()
    {
      s_TemplateNames = NeovimCodeEditor.s_OpenFileArgsTemplates
        .Select(t => t.Name)
        .Append(k_CustomLabel)
        .ToList();
    }

    private void CreateGUI()
    {
      // deep-copy bindings so we don't mutate config until user clicks Update
      m_Bindings = NeovimCodeEditor.s_Config.ModifierBindings
        .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args })
        .ToList();

      // ── root: column (desc on top, panels below) ────────────────────────
      var root = rootVisualElement;
      root.style.flexDirection = FlexDirection.Column;
      root.style.flexGrow = 1;

      var windowDesc = new Label("Configure how Neovim opens files when clicked in Unity, per modifier key:");
      windowDesc.style.whiteSpace = WhiteSpace.Normal;
      windowDesc.style.marginTop = 6;
      windowDesc.style.marginBottom = 6;
      windowDesc.style.marginLeft = 6;
      windowDesc.style.marginRight = 6;
      root.Add(windowDesc);

      // ── panels row ───────────────────────────────────────────────────────
      var panelsRow = new VisualElement();
      panelsRow.style.flexDirection = FlexDirection.Row;
      panelsRow.style.flexGrow = 1;
      root.Add(panelsRow);

      // ── LEFT panel ───────────────────────────────────────────────────────
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

      var scrollView = new ScrollView(ScrollViewMode.Vertical);
      scrollView.style.flexGrow = 1;

      m_BindingRows = new VisualElement();
      m_BindingRows.style.flexDirection = FlexDirection.Column;
      scrollView.Add(m_BindingRows);
      leftPanel.Add(scrollView);

      // populate binding rows
      RebuildBindingRows();

      // bottom toolbar: [+] Add + [Update]
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

      var updateBtn = new Button(() =>
      {
        NeovimCodeEditor.s_Config.ModifierBindings = m_Bindings
          .Select(b => new ModifierBinding { Modifiers = b.Modifiers, Args = b.Args })
          .ToList();
        NeovimCodeEditor.s_Config.Save();
      })
      { text = "Apply" };

      toolbar.Add(addBtn);
      toolbar.Add(updateBtn);
      leftPanel.Add(toolbar);

      // ── RIGHT panel ──────────────────────────────────────────────────────
      var rightPanel = new VisualElement();
      rightPanel.style.width = 220;
      rightPanel.style.flexShrink = 0;
      rightPanel.style.flexDirection = FlexDirection.Column;
      rightPanel.style.paddingLeft = 8;
      rightPanel.style.paddingTop = 8;
      rightPanel.style.paddingRight = 4;

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

      // ── separator ────────────────────────────────────────────────────────
      var separator = new VisualElement();
      separator.style.borderTopWidth = 1;
      separator.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f);
      separator.style.marginTop = 8;
      separator.style.marginBottom = 6;
      rightPanel.Add(separator);

      // ── placeholder reference ─────────────────────────────────────────
      var placeholderTitle = new Label("Placeholders");
      placeholderTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      placeholderTitle.style.marginBottom = 4;
      rightPanel.Add(placeholderTitle);

      var placeholderInfo = new Label(
        "{filePath} — path to the file being opened.\n\n"
        + "{serverSocket} — socket used to communicate with the Neovim server (Unix domain socket on Linux, TCP address on Windows).");
      placeholderInfo.style.whiteSpace = WhiteSpace.Normal;
      placeholderInfo.style.flexWrap = Wrap.Wrap;
      rightPanel.Add(placeholderInfo);

      panelsRow.Add(leftPanel);
      panelsRow.Add(rightPanel);
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

        // ── row header: modifier toggles (skip for default) + delete button ──
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

        // ── template dropdown ─────────────────────────────────────────────
        string currentTemplateName = GetTemplateName(binding.Args);
        var templateDd = new DropdownField(s_TemplateNames, s_TemplateNames.IndexOf(currentTemplateName));
        templateDd.style.marginTop = 4;

        // ── args text field ───────────────────────────────────────────────
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
  }
}
