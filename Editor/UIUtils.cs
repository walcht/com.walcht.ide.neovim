using System.Reflection;
using UnityEngine.UIElements;

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
                UnityEngine.Debug.LogError("Can't change vertical scroller visibility, because method and field not found");
        }
    }
}