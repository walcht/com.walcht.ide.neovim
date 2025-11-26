#if UNITY_EDITOR_LINUX || UNITY_EDITOR_WIN
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;

namespace Neovim.Editor
{
  public class NeovimChangeOpenFileRequestArgs: EditorWindow
  {
      // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
      [MenuItem("Neovim/Change Open-File Request Args")]
      static void Init()
      {
        var window = EditorWindow.GetWindow<NeovimChangeOpenFileRequestArgs>(true, "Change Open-File Request Args");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 600, 200);
        window.minSize = new Vector2(500, 125);
        window.ShowModalUtility();
      }

      // CreateGUI is called when the EditorWindow's rootVisualElement is ready to be populated.
      private void CreateGUI()
      {
          string current_args = EditorPrefs.GetString("NvimUnityOpenFileArgs");

          var label = new Label();

          var args_field = new TextField();
          var templates_dd = new DropdownField(NeovimCodeEditor.s_OpenFileArgsTemplates.ToList(), 0);
          templates_dd.SetValueWithoutNotify("select open-file request args");

          label.text = "Enter custom open-file request args (or choose template):";

          if (current_args == null)
          {
            args_field.value = NeovimCodeEditor.s_OpenFileArgsTemplates[0];
          }
          else
          {
            args_field.value = current_args;
          }

          var msg_field = new TextField();
          msg_field.multiline = true;
          msg_field.isReadOnly = true;
          msg_field.style.flexGrow = 2;
          msg_field.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

          var update_btn = new Button() { text = "Update" };
          update_btn.clicked += () => {
            EditorPrefs.SetString("NvimUnityOpenFileArgs", args_field.value);
            msg_field.value += "[INFO] successfully changed open-file request args\n";
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
#endif
