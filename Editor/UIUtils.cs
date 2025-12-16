#pragma warning disable IDE0130
using System.Reflection;
using UnityEngine.UIElements;

namespace Neovim.Editor
{
  public static class UIUtils
  {
    public static void SafeSetScrollerVisibility(TextField textField, ScrollerVisibility visibility)
    {
      var type = textField.GetType();

      PropertyInfo prop = type.GetProperty("verticalScrollerVisibility");

      if (prop != null)
        prop.SetValue(textField, visibility);
      else
      {
        MethodInfo method = type.GetMethod("SetVerticalScrollerVisibility", new[] { typeof(ScrollerVisibility) });

        if (method != null)
        {
          method.Invoke(textField, new object[] { visibility });
        }
        else
          UnityEngine.Debug.LogError("[neovim.ide] can't change vertical scroller visibility, because method and field not found");
      }
    }
  }
}
