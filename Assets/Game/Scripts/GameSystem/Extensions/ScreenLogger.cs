using UnityEngine;
using UnityEngine.UI;

namespace Gameplay
{
    public static class ScreenLogger
    {
        private static Text debugText;
        private static GameObject canvasObject;

        public static void Log(string message, Color? color = null, int fontSize = 18)
        {
            if (debugText == null)
                CreateUI();

            Color col = color ?? Color.white;
            debugText.supportRichText = true;
            debugText.text +=
                $"<size={fontSize}><color=#{ColorUtility.ToHtmlStringRGB(col)}>{message}</color></size>\n";
        }

        private static void CreateUI()
        {
            canvasObject = new GameObject("OnScreenLoggerCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject textObject = new GameObject("DebugText");
            textObject.transform.SetParent(canvasObject.transform);
            debugText = textObject.AddComponent<Text>();

            // ✅ Updated font loading
            debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            debugText.alignment = TextAnchor.UpperLeft;
            debugText.horizontalOverflow = HorizontalWrapMode.Wrap;
            debugText.verticalOverflow = VerticalWrapMode.Overflow;
            debugText.color = Color.white;
            debugText.fontSize = 18;

            RectTransform rect = debugText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(10, 10);
            rect.offsetMax = new Vector2(-10, -10);
        }
    }
}