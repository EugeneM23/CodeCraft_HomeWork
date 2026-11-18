using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// Rebuilds every UI asset in order: placeholder data, menu prefab and scene, battle prefab.
    /// Also the entry point for batch mode: -executeMethod Game.EditorTools.UiAllBuilder.Build
    /// </summary>
    public static class UiAllBuilder
    {
        [MenuItem("Tools/UI/Rebuild All")]
        public static void Build()
        {
            UiDataBuilder.Build();
            UiIconSetBuilder.Ensure();
            MenuUiBuilder.Build();
            MenuUiBuilder.BuildScene();
            BattleUiBuilder.Build();
            UiDataBuilder.AddBattleUiToLevelLayer();
            AppPrefabUiBuilder.Build();

            // The fonts are dynamic: glyphs are added when text is drawn. Saved empty, they don't put
            // megabytes of atlas into every commit.
            // Play draws the screens, Roboto behind it the signs Play lacks: all four dynamic.
            TMPro.TMP_FontAsset[] fonts = { UiFactory.Regular, UiFactory.Bold, UiFactory.FallbackRegular, UiFactory.FallbackBold };
            foreach (TMPro.TMP_FontAsset font in fonts)
            {
                font.ClearFontAssetData(true);
                EditorUtility.SetDirty(font);
            }
            AssetDatabase.SaveAssets();

            // The builders make and drop temporary objects in the open scene; when that is the menu scene we
            // just built, leave it saved: a dirty scene makes the test runner stop on a save dialog.
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path == MenuUiBuilder.SCENE_PATH && active.isDirty)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active);
        }
    }
}
