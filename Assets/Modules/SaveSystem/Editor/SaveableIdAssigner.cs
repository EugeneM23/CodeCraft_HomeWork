using Saving.Objects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Editor
{
    /// <summary>
    /// Ids of scene Saveables in the editor: a new one when empty or used by another Saveable in the open scenes,
    /// written as a prefab instance override with Undo. Prefab assets and objects in prefab mode have no id: their
    /// instances get one in the scene, copies made in play get one in Awake.
    /// </summary>
    [InitializeOnLoad]
    public static class SaveableIdAssigner
    {
        static SaveableIdAssigner()
        {
            Saveable.EditorValidate = OnValidate;
            // A freshly added component may be changed at once: no wait for an editor update, which a background
            // editor hardly runs. Shared with SaveSystemSetup: both add to it
            Saveable.EditorAdded += saveable => Assign(saveable);
        }

        private static void OnValidate(Saveable saveable)
        {
            // OnValidate must not change serialized data: do it right after
            EditorApplication.delayCall += () =>
            {
                if (saveable != null)
                    Assign(saveable);
            };
        }

        public static bool Assign(Saveable saveable)
        {
            if (saveable == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            var serialized = new SerializedObject(saveable);
            SerializedProperty id = serialized.FindProperty("_id");
            if (IsPrefabSide(saveable))
            {
                if (string.IsNullOrEmpty(id.stringValue))
                    return false;

                id.stringValue = string.Empty;
            }
            else
            {
                if (!string.IsNullOrEmpty(id.stringValue) && !IsUsedElsewhere(saveable, id.stringValue))
                    return false;

                id.stringValue = SaveRegistry.NewId();
            }

            serialized.ApplyModifiedProperties();
            return true;
        }

        public static bool IsPrefabSide(Saveable saveable) =>
            PrefabUtility.IsPartOfPrefabAsset(saveable) || PrefabStageUtility.GetPrefabStage(saveable.gameObject) != null;

        public static bool IsUsedElsewhere(Saveable saveable, string id)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Saveable other in root.GetComponentsInChildren<Saveable>(true))
                    {
                        if (other != saveable && other.Id == id)
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
