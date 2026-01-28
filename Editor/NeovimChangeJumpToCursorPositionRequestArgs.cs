#pragma warning disable IDE0130
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;

namespace Neovim.Editor
{
  public class NeovimChangeJumpToCursorPositionRequestArgs : EditorWindow
  {
    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    [MenuItem("Neovim/Change Jump-to-Cursor-Position Request Args")]
    static void Init()
    {
      var window = GetWindow<NeovimChangeJumpToCursorPositionRequestArgs>(true, "Change Jump-to-Cursor-Position Request Args");
      window.position = new Rect(Screen.width / 2, Screen.height / 2, 600, 250);
      window.minSize = new Vector2(500, 250);
      window.ShowModalUtility();
    }

    // CreateGUI is called when the EditorWindow's rootVisualElement is ready to be populated.
    private void CreateGUI()
    {
      string current_args = NeovimCodeEditor.s_Config.JumpToCursorPositionArgs;

      var label = new Label();

      var args_field = new TextField()
      {
        label = "arguments",
        tooltip = "Arguments to be passed to NeoVim when a cursor position jump to particular file is requested. "
          + "Text between {} if for special placeholders (read below)."
      };
      var templates_dd = new DropdownField(NeovimCodeEditor.s_JumpToCursorPositionArgsTemplates.ToList(), 0);
      templates_dd.SetValueWithoutNotify("select template");

      label.text = "Enter custom jump-to-cursor-position request args (or choose template):";

      args_field.value = current_args;

      var msg_field = new TextField
      {
        multiline = true,
        isReadOnly = true
      };
      msg_field.style.flexGrow = 2;
      UIUtils.SafeSetScrollerVisibility(msg_field, ScrollerVisibility.AlwaysVisible);

      // add explanation for placeholders
      msg_field.value =
        "{serverSocket} - is replaced by the socket that is used to communicate with the Neovim server instance (TCP socket on Windows and Unix Domain socket path on Linux).\n"
        + "{line} - is replaced by the line number that was requested to jump into.\n"
        + "{column} is replaced by the column number that was requested to jump into.\n\n";

      var update_btn = new Button() { text = "Update" };
      update_btn.clicked += () =>
      {
        NeovimCodeEditor.s_Config.JumpToCursorPositionArgs = args_field.value;
        NeovimCodeEditor.s_Config.Save();
        msg_field.value += "[INFO] successfully changed jump-to-cursor-position request args\n";
      };

      templates_dd.RegisterValueChangedCallback(e =>
      {
        args_field.value = e.newValue;
      });

      rootVisualElement.Add(label);
      rootVisualElement.Add(templates_dd);
      rootVisualElement.Add(args_field);
      rootVisualElement.Add(update_btn);
      rootVisualElement.Add(msg_field);
    }
  }
}
