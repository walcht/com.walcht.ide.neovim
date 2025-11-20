#if UNITY_EDITOR_LINUX || UNITY_EDITOR_WIN
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

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
          string currentTermLaunchCmd = EditorPrefs.GetString("NvimUnityTermLaunchCmd");
          string currentTermLaunchArgs = EditorPrefs.GetString("NvimUnityTermLaunchArgs");

          var label = new Label();

          var termLaunchCmdField = new TextField();
          var termLaunchArgsField = new TextField();

          if (currentTermLaunchCmd == null) {
            (string templateCmd, string templateArgs) = NeovimCodeEditor.s_TermLaunchCmdTemplate;
            termLaunchCmdField.value = templateCmd;
            termLaunchArgsField.value = templateArgs;
            label.text = "Enter custom terminal launch cmd & args:";
          } else {
            termLaunchCmdField.value = currentTermLaunchCmd;
            termLaunchArgsField.value = currentTermLaunchArgs;
            label.text = "Current terminal launch cmd & args:";
          }

          var msgField = new TextField();
          msgField.multiline = true;
          msgField.isReadOnly = true;
          msgField.style.flexGrow = 2;
          // without this crap you can't stretch the stupid TextField... well done Unity,
          // after all these years a basic task such as this took me fucking 3 hours
          msgField.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;

          var updateBtn = new Button() { text = "Update" };
          updateBtn.clicked += () => OnAddTermLaunchCmd(termLaunchCmdField, termLaunchArgsField, msgField);

          rootVisualElement.Add(label);
          rootVisualElement.Add(termLaunchCmdField);
          rootVisualElement.Add(termLaunchArgsField);
          rootVisualElement.Add(updateBtn);
          rootVisualElement.Add(msgField);
      }

      private void OnAddTermLaunchCmd(TextField termLaunchCmd, TextField termLaunchArgs, TextField msgField) {
        if (!NeovimCodeEditor.TryChangeTermLaunchCmd((termLaunchCmd.value, termLaunchArgs.value))) {
          msgField.value = "[ERROR] provided terminal is not available\n"
            + $"[ERROR] 'which {termLaunchCmd.value}' returned !=0 value\n";
          return;
        }
        msgField.value = "[INFO] successfully changed terminal launch command\n";
      }
  }
}
#endif
