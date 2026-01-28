#pragma warning disable IDE0130
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;

namespace Neovim.Editor
{
  public class NeovimChangeProcessTimeout : EditorWindow
  {
    // MenuItem Creates a menu item and invokes the static function that follows it when the menu item is selected.
    [MenuItem("Neovim/Change Process Timeout")]
    static void Init()
    {
      var window = GetWindow<NeovimChangeProcessTimeout>(true, "Change Process Timeout");
      window.position = new Rect(Screen.width / 2, Screen.height / 2, 600, 225);
      window.minSize = new Vector2(500, 225);
      window.ShowModalUtility();
    }

    // CreateGUI is called when the EditorWindow's rootVisualElement is ready to be populated.
    private void CreateGUI()
    {
      var label = new Label()
      {
        text = "Change process timeout (ms):"
      };

      var intField = new IntegerField()
      {
        label = "timeout (ms)",
        tooltip = "Process timeout afterwhich the process is killed. Read below for further details.",
        value = NeovimCodeEditor.s_Config.ProcessTimeout
      };

      var msgField = new TextField
      {
        multiline = true,
        isReadOnly = true
      };
      msgField.style.flexGrow = 2;

      UIUtils.SafeSetScrollerVisibility(msgField, ScrollerVisibility.AlwaysVisible);

      // add explanation for placeholders
      msgField.value =
        "Process timeout is the maximum time in milliseconds to wait for the\n"
        + "spawned process to finish. If the process does not finish within this\n"
        + "timeout, it will be killed. This timeout is used when spawning the\n"
        + "open-file request process, the jumo-to-cursor-position request process,\n"
        + "and the focus-on-neovim process.\n"
        + "Smaller values result in smoother experience at the cost of a potential\n"
        + "process being killed before it finishes its execution.";

      var updateBtn = new Button() { text = "Update Timeout" };
      updateBtn.clicked += () =>
      {
        if (intField.value <= 0)
        {
          msgField.value += "[ERROR] cannot set a 0 timeout value (infinite timeout will freeze the Unity Editor)";
          intField.SetValueWithoutNotify(NeovimCodeEditor.s_Config.ProcessTimeout);
          return;
        }

        if (intField.value > 1000)
        {
          msgField.value += "[ERROR] cannot set a timeout higher than 1s (will freeze the Unity Editor)";
          intField.SetValueWithoutNotify(NeovimCodeEditor.s_Config.ProcessTimeout);
          return;
        }

        intField.value = NeovimCodeEditor.s_Config.ProcessTimeout = intField.value;
        msgField.value += $"[INFO] updated process timeout to: {NeovimCodeEditor.s_Config.ProcessTimeout}";
      };

      rootVisualElement.Add(label);
      rootVisualElement.Add(intField);
      rootVisualElement.Add(updateBtn);
      rootVisualElement.Add(msgField);
    }
  }
}
