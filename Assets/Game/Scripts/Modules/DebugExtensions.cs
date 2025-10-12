using System;
using UnityEngine;

namespace Gameplay
{
    public static class DebugExtensions
    {
        public static T Log<T>(this T value)
        {
            Log(value, null, "");
            return value;
        }

        public static T Log<T>(this T value, string massage)
        {
            Log(value, null, massage);
            return value;
        }

        public static T Log<T>(this T value, Color? color)
        {
            Log(value, color, "");
            return value;
        }

        public static T Log<T>(this T value, object context)
        {
            Log(value, null, context.ToString());
            return value;
        }

        public static T Log<T>(this T value, Color? color = null, string massage = "")
        {
            Color finalColor = color ?? Color.white;
            string htmlColor = ColorUtility.ToHtmlStringRGB(finalColor);
            Debug.Log($"<color=#{htmlColor}><b>{massage} --> {value}</b></color>");
            return value;
        }
    }
}