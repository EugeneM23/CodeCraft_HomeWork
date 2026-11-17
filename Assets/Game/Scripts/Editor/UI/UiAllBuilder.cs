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
            AssetDatabase.SaveAssets();
        }
    }
}
