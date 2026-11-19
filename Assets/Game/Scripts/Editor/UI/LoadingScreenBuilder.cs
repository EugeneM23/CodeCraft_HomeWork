using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Rebuilds the core's loading screen (App prefab → LoadingScreen) in the menus' look: the dark cyan background,
    /// a «ЗАГРУЗКА» title between the pack's title lines, the pack's progress bar (a filled image the screen drives)
    /// and a muted hint. Only the screen's children change; its CanvasGroup and the LoadingScreen component stay.
    /// </summary>
    public static class LoadingScreenBuilder
    {
        private const string APP_PREFAB = "Assets/Game/Prefabs/App/App.prefab";

        [MenuItem("Tools/UI/Build Loading Screen")]
        public static void Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(APP_PREFAB);
            try
            {
                LoadingScreen screen = root.GetComponentInChildren<LoadingScreen>(true);
                if (screen == null)
                {
                    Debug.LogError("[LoadingScreenBuilder] No LoadingScreen in " + APP_PREFAB);
                    return;
                }

                Transform t = screen.transform;
                // The screen covers the whole canvas (it was a zero-size rect: its parts sat in a corner).
                if (t is RectTransform screenRect)
                    UiFactory.Stretch(screenRect);
                for (int i = t.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(t.GetChild(i).gameObject);
                // The screen's own Image (an old black backdrop), if any, takes the background colour.
                if (screen.TryGetComponent(out Image own))
                    own.color = UiStyle.Background;

                Image background = UiFactory.Panel("Background", t, UiStyle.Background);
                UiFactory.Stretch(background.rectTransform);
                background.raycastTarget = true;

                Image vignette = UiFactory.Frame("Frame", t, UiPackSprites.Panel, new Color(1f, 1f, 1f, 0.35f));
                UiFactory.Stretch(vignette.rectTransform, 60f, 60f, 60f, 60f);
                vignette.raycastTarget = false;

                TextMeshProUGUI title = UiFactory.Text("Title", t, "ЗАГРУЗКА", UiStyle.FONT_TITLE + 8f,
                    TextAlignmentOptions.Center, true, UiStyle.Accent);
                title.characterSpacing = 12f;
                UiFactory.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(700f, 80f));

                Image lineLeft = UiFactory.Frame("LineLeft", t, UiPackSprites.TitleLineLeft, UiStyle.Accent);
                UiFactory.Place(lineLeft.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-430f, 70f), new Vector2(220f, 18f));
                Image lineRight = UiFactory.Frame("LineRight", t, UiPackSprites.TitleLineRight, UiStyle.Accent);
                UiFactory.Place(lineRight.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(430f, 70f), new Vector2(220f, 18f));

                Image back = UiFactory.Frame("BarBack", t, UiPackSprites.ProgressBack);
                UiFactory.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(760f, 30f));
                Image fill = UiFactory.Frame("BarFill", back.transform, UiPackSprites.ProgressFill, UiStyle.Accent);
                UiFactory.Stretch(fill.rectTransform, 4f, 4f, 4f, 4f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = (int)Image.OriginHorizontal.Left;
                fill.fillAmount = 0f;
                fill.raycastTarget = false;

                TextMeshProUGUI hint = UiFactory.Text("Hint", t, "Подготовка поля боя…", UiStyle.FONT_BODY,
                    TextAlignmentOptions.Center, false, UiStyle.TextMuted);
                UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(760f, 40f));

                var so = new SerializedObject(screen);
                so.FindProperty("_barFill").objectReferenceValue = fill;
                so.FindProperty("_barSlider").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, APP_PREFAB);
                Debug.Log("[LoadingScreenBuilder] Loading screen rebuilt in " + APP_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
