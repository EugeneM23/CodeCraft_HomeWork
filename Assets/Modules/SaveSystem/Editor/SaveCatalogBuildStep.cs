using Saving.Objects;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Saving.Editor
{
    /// <summary>Before a build: prefab ids and catalogs up to date, so the player finds every prefab and scene.</summary>
    internal sealed class SaveCatalogBuildStep : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            SaveCatalogBuilder.BakePrefabIds();
            foreach (SaveSystemSettings settings in SaveCatalogBuilder.AllSettings())
                SaveCatalogBuilder.Rebuild(settings);
        }
    }
}
