#pragma warning disable IDE0130
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;
using System.Collections.Generic;

namespace Neovim.Editor
{
  public class NeovimChangeJumpToCursorPositionRequestArgs : EditorWindow
  {
    private Button m_ApplyBtn = null;
    private TextField m_ArgsTf;
    private VisualElement m_CenterArea;
    private bool m_Dirty;
    private static readonly List<string> s_TemplateNames;
    private static readonly string k_CustomLabel = "Custom";

    // TODO: persistent window position
    static NeovimChangeJumpToCursorPositionRequestArgs() {
      s_TemplateNames = NeovimCodeEditor.s_JumpToCursorPositionArgsTemplates
        .Select(t => t.Name)
        .Append(k_CustomLabel)
        .ToList();
    }

    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    [MenuItem("Neovim/Change Jump-to-Cursor-Position Request Args")]
    static void Init()
    {
      var window = GetWindow<NeovimChangeJumpToCursorPositionRequestArgs>(true, "Change Jump-to-Cursor-Position Request Args");
      window.position = new Rect(Screen.width / 2, Screen.height / 2, 800, 200);
      window.minSize = new Vector2(600, 200);
      window.saveChangesMessage = "This window has unsaved changes. Would you like to save?";
      window.ShowModalUtility();
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
      m_Dirty = val;
      if (m_ApplyBtn != null)
        m_ApplyBtn.SetEnabled(val);
    }

    // CreateGUI is called when the EditorWindow's rootVisualElement is ready to be populated.
    private void CreateGUI()
    {
      string currArgs = NeovimCodeEditor.s_Config.JumpToCursorPositionArgs;
      string currentTemplateName = GetTemplateName(currArgs);

      // ── root: column (desc on top, panels below) ────────────────────────
      var root = rootVisualElement;
      root.style.flexDirection = FlexDirection.Column;
      root.style.flexGrow = 1;

      var windowDesc = new Label("Configure how Neovim jumps to requested cursor position:");
      windowDesc.style.whiteSpace = WhiteSpace.Normal;
      windowDesc.style.unityFontStyleAndWeight = FontStyle.Bold;
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
      leftPanel.style.borderRightColor = new Color(0.4f, 0.4f, 0.4f);
      leftPanel.style.paddingRight = 4;

      var templatesDd = new DropdownField(s_TemplateNames, s_TemplateNames.IndexOf(currentTemplateName))
      { label = "Template:" };

      m_ArgsTf = new TextField()
      {
        label = "Arguments:",
        tooltip = "Arguments to be passed to NeoVim when a cursor position jump to particular file is requested. "
          + "Text between {} if for special placeholders (read below).",
        value = currArgs
      };

      m_ArgsTf.RegisterValueChangedCallback(e => {
        if (e.newValue == NeovimCodeEditor.s_Config.JumpToCursorPositionArgs)
          return;
        SetDirty(true);
      });

      templatesDd.RegisterValueChangedCallback(e =>
      {
        SetDirty(true);
        if (e.newValue == k_CustomLabel)
        {
          // SetInfoPanel(null);
          return;
        }
        var template = NeovimCodeEditor.s_JumpToCursorPositionArgsTemplates
          .FirstOrDefault(t => t.Name == e.newValue);
        if (template.Name == null) return;
        m_ArgsTf.SetValueWithoutNotify(template.Args);
        // SetInfoPanel(template);
      });

      var spacing = new VisualElement();
      spacing.style.flexGrow = 1;

      leftPanel.Add(templatesDd);
      leftPanel.Add(m_ArgsTf);
      leftPanel.Add(spacing);

      var toolbar = new VisualElement();
      toolbar.style.flexDirection = FlexDirection.Row;
      toolbar.style.flexShrink = 0;
      toolbar.style.justifyContent = Justify.SpaceBetween;
      toolbar.style.marginTop = 6;
      toolbar.style.marginBottom = 4;
      toolbar.style.marginLeft = 4;
      toolbar.style.marginRight = 4;

      m_ApplyBtn = new Button();
      m_ApplyBtn.text = "Apply";
      m_ApplyBtn.clicked += OnApplyClick;

      toolbar.Add(m_ApplyBtn);
      leftPanel.Add(toolbar);

      // ── RIGHT panel ──────────────────────────────────────────────────────
      var rightPanel = new VisualElement();
      rightPanel.style.width = 220;
      rightPanel.style.flexShrink = 0;
      rightPanel.style.flexDirection = FlexDirection.Column;
      rightPanel.style.paddingLeft = 8;
      rightPanel.style.paddingTop = 8;
      rightPanel.style.paddingRight = 4;

      // ── placeholder reference ─────────────────────────────────────────
      var placeholderTitle = new Label("Placeholders");
      placeholderTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
      placeholderTitle.style.marginBottom = 4;

      var placeholderInfo = new Label(
        "<b>{serverSocket}</b> — communication socket with the Neovim server instance (TCP socket on Windows, Unix Domain socket path on Linux).\n\n"
        + "<b>{line}</b> — line number.\n\n"
        + "<b>{column}</b> — column number.");
      placeholderInfo.style.whiteSpace = WhiteSpace.Normal;
      placeholderInfo.style.flexWrap = Wrap.Wrap;

      rightPanel.Add(placeholderTitle);
      rightPanel.Add(placeholderInfo);

      panelsRow.Add(leftPanel);
      panelsRow.Add(rightPanel);

      SetDirty(false);
    }

    private void Save()
    {
      NeovimCodeEditor.s_Config.JumpToCursorPositionArgs = m_ArgsTf.value;
      NeovimCodeEditor.s_Config.Save();
      SetDirty(false);
    }

    private void OnApplyClick() => Save();

    private static string GetTemplateName(string args)
    {
      var match = NeovimCodeEditor.s_JumpToCursorPositionArgsTemplates
        .FirstOrDefault(t => t.Args == args);
      return match.Name ?? k_CustomLabel;
    }

  }
}
