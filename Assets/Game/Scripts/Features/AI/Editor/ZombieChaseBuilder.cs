using Atomic.Entities;
using UnityEditor;
using UnityEngine;

namespace Game.AIKit.Adapters.EditorTools
{
    /// <summary>
    /// Gives <c>Zombie.prefab</c> its brain (Tools/AI/Give Zombies Chase): <see cref="ZombieChaseInstaller"/> on the root,
    /// listed after the zombie's own installers. Run again — nothing changes. Batchmode:
    /// <c>-executeMethod Game.AIKit.Adapters.EditorTools.ZombieChaseBuilder.Build</c>.
    /// </summary>
    public static class ZombieChaseBuilder
    {
        private const string LOG = "[ZombieChase]";
        public const string PREFAB = "Assets/Game/Prefabs/Characters/Zombie.prefab";

        [MenuItem("Tools/AI/Give Zombies Chase")]
        public static void Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB);
            try
            {
                MonoEntity entity = root.GetComponent<MonoEntity>();
                if (entity == null || root.GetComponent<EnemyCharacterCoreInstaller>() == null)
                {
                    Debug.LogError($"{LOG} {PREFAB} has no MonoEntity or EnemyCharacterCoreInstaller on its root");
                    return;
                }

                if (!root.TryGetComponent(out ZombieChaseInstaller installer))
                    installer = root.AddComponent<ZombieChaseInstaller>();

                var serialized = new SerializedObject(entity);
                SerializedProperty list = serialized.FindProperty("sceneInstallers");
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == installer)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
                        Debug.Log($"{LOG} {PREFAB} already chases");
                        return;
                    }
                }

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
                Debug.Log($"{LOG} {PREFAB}: ZombieChaseInstaller added and listed");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
