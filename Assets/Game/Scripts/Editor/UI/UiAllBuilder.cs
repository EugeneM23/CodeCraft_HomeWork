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
            MenuUiBuilder.Build();
            MenuUiBuilder.BuildScene();
            BattleUiBuilder.Build();
            UiDataBuilder.AddBattleUiToLevelLayer();
            AppPrefabUiBuilder.Build();

            // The fonts are dynamic: glyphs are added when text is drawn. Saved empty, they don't put
            // megabytes of atlas into every commit.
            UiFactory.Regular.ClearFontAssetData(true);
            UiFactory.Bold.ClearFontAssetData(true);
            EditorUtility.SetDirty(UiFactory.Regular);
            EditorUtility.SetDirty(UiFactory.Bold);
            AssetDatabase.SaveAssets();

            // The builders make and drop temporary objects in the open scene; when that is the menu scene we
            // just built, leave it saved: a dirty scene makes the test runner stop on a save dialog.
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.path == MenuUiBuilder.SCENE_PATH && active.isDirty)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(active);
        }
    }
}
