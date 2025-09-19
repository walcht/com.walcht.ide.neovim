using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

#if UNITY_EDITOR_LINUX
namespace Neovim.Editor
{
  public class NeovimTerminalLaunchCmd : EditorWindow
  {
      public static void RequestTerminalLaunchCmdChange() {
          var window = EditorWindow.GetWindow<NeovimTerminalLaunchCmd>(true, "Neovim Terminal Launch Command");
          window.position = new Rect(Screen.width / 2, Screen.height / 2, 600, 125);
          window.minSize = new Vector2(500, 125);
          window.ShowModalUtility();
      }


      // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
      [MenuItem("Neovim/Change Terminal Launch Cmd")]
      static void Init()
      {
        RequestTerminalLaunchCmdChange();
      }

      // CreateGUI is called when the EditorWindow's rootVisualElement is ready to be populated.
      private void CreateGUI()
      {
          string currentTermLaunchCmd = NeovimCodeEditor.GetDefaultTermLaunchCmd();

          var label = new Label();

          var termLaunchCmdField = new TextField();

          if (currentTermLaunchCmd == null) {
            termLaunchCmdField.value = NeovimCodeEditor.s_TermLaunchCmdTemplate;
            label.text = "Enter custom terminal launch command:";
          } else {
            termLaunchCmdField.value = currentTermLaunchCmd;
            label.text = "Current terminal launch command:";
          }

          var msgField = new TextField();
          msgField.multiline = true;
          msgField.isReadOnly = true;
          msgField.style.flexGrow = 2;
          // without this crap you can't stretch the stupid TextField... well done Unity,
          // after all these years a basic task such as this took me fucking 3 hours
          msgField.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

          var updateBtn = new Button() { text = "Update" };
          updateBtn.clicked += () => OnAddTermLaunchCmd(termLaunchCmdField, msgField);

          rootVisualElement.Add(label);
          rootVisualElement.Add(termLaunchCmdField);
          rootVisualElement.Add(updateBtn);
          rootVisualElement.Add(msgField);
      }

      private void OnAddTermLaunchCmd(TextField termLaunchCmd, TextField msgField) {
        if (!NeovimCodeEditor.ChangeTermLaunchCmd(termLaunchCmd.value)) {
          msgField.value = "[ERROR] provided terminal is not available\n"
            + $"[ERROR] 'which {termLaunchCmd.value}' returned !=0 value\n";
          return;
        }
        msgField.value = "[INFO] successfully changed terminal launch command\n";
      }
  }
}
#endif
