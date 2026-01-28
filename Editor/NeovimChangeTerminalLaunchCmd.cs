#pragma warning disable IDE0130
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;

namespace Neovim.Editor
{
  public class NeovimChangeTerminalLaunchCmd : EditorWindow
  {
    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    [MenuItem("Neovim/Change Terminal Launch Cmd")]
    static void Init()
    {
      var window = GetWindow<NeovimChangeTerminalLaunchCmd>(true, "Change Terminal Launch Command");
      window.position = new Rect(Screen.width / 2, Screen.height / 2, 600, 225);
      window.minSize = new Vector2(500, 125);
      window.ShowModalUtility();
    }

    // CreateGUI is called when the EditorWindow's rootVisualElement is ready to be populated.
    private void CreateGUI()
    {
      string currentTermLaunchCmd = NeovimCodeEditor.s_Config.TermLaunchCmd;
      string currentTermLaunchArgs = NeovimCodeEditor.s_Config.TermLaunchArgs;
      string currentTermLaunchEnv = NeovimCodeEditor.s_Config.TermLaunchEnv;

      var label = new Label();

      var termLaunchCmdField = new TextField();
      var termLaunchArgsField = new TextField();
      var termLaunchEnvField = new TextField();
      var termLaunchTemplates = new DropdownField(NeovimCodeEditor.s_TermLaunchCmds
          .Select((cmdargs, _) => cmdargs.Item1).ToList(), 0);
      termLaunchTemplates.SetValueWithoutNotify("select template");

      label.text = "Enter custom terminal launch cmd & args (or choose template):";

      if (currentTermLaunchCmd == null)
      {
        (string templateCmd, string templateArgs, string templateEnv) = NeovimCodeEditor.s_TermLaunchCmdTemplate;
        termLaunchCmdField.value = templateCmd;
        termLaunchArgsField.value = templateArgs;
        termLaunchEnvField.value = templateEnv;
      }
      else
      {
        termLaunchCmdField.value = currentTermLaunchCmd;
        termLaunchArgsField.value = currentTermLaunchArgs;
        termLaunchEnvField.value = currentTermLaunchEnv;
      }

      var msgField = new TextField
      {
        multiline = true,
        isReadOnly = true
      };
      msgField.style.flexGrow = 2;
      // without this crap you can't stretch the stupid TextField... well done Unity,
      // after all these years a basic task such as this took me fucking 3 hours
      UIUtils.SafeSetScrollerVisibility(msgField, ScrollerVisibility.AlwaysVisible);

      // add explanation for placeholders
      msgField.value =
          "{app} - is replaced by the current chosen Neovim path.\n"
        + "{filePath} - is replaced by the path to requested file to be opened.\n"
#if UNITY_EDITOR_WIN
        + "{getProcessPPIDScriptPath} - is replaced by the path to the GetProcessPPID.ps1 Powershell script which is used to determine the parent process ID which is then used for auto window focusing.\n"
#endif
        + "{serverSocket} - is replaced by the socket that is used to communicate with the Neovim server instance (TCP socket on Windows and Unix Domain socket path on Linux).\n"
        + "{environment} - is empty by default (linux only).\n\n";

      var updateBtn = new Button() { text = "Update" };
      updateBtn.clicked += () => OnAddTermLaunchCmd(termLaunchCmdField, termLaunchArgsField, termLaunchEnvField, msgField);
      termLaunchTemplates.RegisterValueChangedCallback(e =>
      {
        string cmd = e.newValue;
        string args = NeovimCodeEditor.s_TermLaunchCmds.First(cmdargs => cmdargs.Item1 == cmd).Item2;
        string env = NeovimCodeEditor.s_TermLaunchCmds.First(cmdargs => cmdargs.Item1 == cmd).Item3;
        termLaunchCmdField.value = cmd;
        termLaunchArgsField.value = args;
        termLaunchEnvField.value = env;
      });

      rootVisualElement.Add(label);
      rootVisualElement.Add(termLaunchTemplates);
      rootVisualElement.Add(termLaunchCmdField);
      rootVisualElement.Add(termLaunchArgsField);
      rootVisualElement.Add(termLaunchEnvField);
      rootVisualElement.Add(updateBtn);
      rootVisualElement.Add(msgField);
    }

    private void OnAddTermLaunchCmd(TextField termLaunchCmd, TextField termLaunchArgs, TextField termLaunchEnv, TextField msgField)
    {
      if (!NeovimCodeEditor.TryChangeTermLaunchCmd((termLaunchCmd.value, termLaunchArgs.value, termLaunchEnv.value)))
      {
        msgField.value += "[ERROR] provided terminal is not available\n"
          + $"[ERROR] 'which {termLaunchCmd.value}' returned !=0 value\n";
        return;
      }
      msgField.value += "[INFO] successfully changed terminal launch command\n";
    }
  }
}
