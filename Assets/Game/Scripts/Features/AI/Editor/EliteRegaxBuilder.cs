using AIKit;
using Atomic.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.AIKit.Adapters.EditorTools
{
    /// <summary>
    /// Builds the elite Regax reproducibly (Tools/AI/Build Regax Elite):
    /// <list type="bullet">
    /// <item><c>Configs/AI/Regax_Elite_Bite</c> — the Bite ability the graph names (a Code Runner: the game plays it);</item>
    /// <item><c>Configs/AI/Regax_Elite_Senses</c> — what it sees with (far: it hunts a tank);</item>
    /// <item><c>Configs/AI/Regax_Elite_Brain</c> — the graph (<see cref="EliteBrain"/>), written into the asset so the
    /// AIKit graph window opens it; a rebuild keeps the layout of the nodes it already has;</item>
    /// <item><c>Prefabs/Characters/Regax_Elite</c> — a prefab variant of Regax:
    /// <see cref="HEALTH"/> health, a kinematic body, an AI Character (Manual, <see cref="EliteRegaxBody"/>, the graph),
    /// <see cref="EliteRegaxInstaller"/> listed after the Regax's installers, and <see cref="AtomicHitReceiver"/>.</item>
    /// </list>
    /// Regax.prefab itself is not touched. Tools/AI/Add Elite Regax To Sandbox puts a few into the open scene (not saved).
    /// </summary>
    public static class EliteRegaxBuilder
    {
        private const string LOG = "[EliteRegax]";
        private const string FOLDER = "Assets/Game/Configs/AI";
        private const string BITE = FOLDER + "/Regax_Elite_Bite.asset";
        private const string SENSES = FOLDER + "/Regax_Elite_Senses.asset";
        private const string BRAIN = FOLDER + "/Regax_Elite_Brain.asset";
        private const string BASE_PREFAB = "Assets/Game/Prefabs/Characters/Regax.prefab";
        public const string PREFAB = "Assets/Game/Prefabs/Characters/Regax_Elite.prefab";

        public const int HEALTH = 300;

        /// <summary>Elites the sandbox menu places, and how far from the tank.</summary>
        private const int SANDBOX_COUNT = 3;
        private const float SANDBOX_DISTANCE = 25f;

        [MenuItem("Tools/AI/Build Regax Elite")]
        public static void Build()
        {
            EnsureFolder();
            AbilityAsset bite = SaveOrUpdate(EliteBrain.CreateBite(), BITE);
            PerceptionProfile senses = SaveOrUpdate(CreateSenses(), SENSES);
            BehaviorGraphAsset brain = BuildBrain(bite, senses);
            if (BuildPrefab(brain, senses))
                Debug.Log($"{LOG} built {PREFAB} with {BRAIN}");
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/AI/Add Elite Regax To Sandbox")]
        public static void AddToOpenScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} no {PREFAB}: run Tools/AI/Build Regax Elite first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            Transform tank = FindTank();
            Vector3 center = tank != null ? tank.position : Vector3.zero;
            Vector3 forward = tank != null ? tank.forward : Vector3.forward;
            for (int i = 0; i < SANDBOX_COUNT; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(-40f + 40f * i, Vector3.up) * forward;
                Vector3 position = center + direction * SANDBOX_DISTANCE;
                if (Physics.Raycast(position + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f,
                        (1 << PhysicsLayers.Ground) | 1, QueryTriggerInteraction.Ignore))
                    position = hit.point;

                var elite = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                elite.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-direction));
                Undo.RegisterCreatedObjectUndo(elite, "Add Elite Regax");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"{LOG} {SANDBOX_COUNT} elites placed in {scene.name} (not saved).");
        }

        // ------------------------------------------------------------------ assets

        private static PerceptionProfile CreateSenses()
        {
            var senses = ScriptableObject.CreateInstance<PerceptionProfile>();
            senses.EyeHeight = 2.2f;
            senses.FocusRange = 45f;
            senses.FocusHalfAngle = 60f;
            senses.PeripheryRange = 25f;
            senses.PeripheryHalfAngle = 120f;
            senses.BackRange = 10f;
            senses.LoseRange = 55f;
            senses.GainPerSecond = 3f;
            senses.MemoryTime = 6f;
            return senses;
        }

        private static BehaviorGraphAsset BuildBrain(AbilityAsset bite, PerceptionProfile senses)
        {
            var asset = AssetDatabase.LoadAssetAtPath<BehaviorGraphAsset>(BRAIN);
            bool created = asset == null;
            if (created)
                asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();

            GraphSerializer.Write(EliteBrain.Build(bite, senses), asset);
            if (created)
                AssetDatabase.CreateAsset(asset, BRAIN);
            else
                EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>Saves a new asset at the path, or copies it into the one there (its GUID and the links to it kept).</summary>
        private static T SaveOrUpdate<T>(T fresh, string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                fresh.name = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }

            string keepName = existing.name;
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = keepName;
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Configs"))
                AssetDatabase.CreateFolder("Assets/Game", "Configs");
            if (!AssetDatabase.IsValidFolder(FOLDER))
                AssetDatabase.CreateFolder("Assets/Game/Configs", "AI");
        }

        // ------------------------------------------------------------------ prefab

        private static bool BuildPrefab(BehaviorGraphAsset brain, PerceptionProfile senses)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(BASE_PREFAB);
            if (source == null)
            {
                Debug.LogError($"{LOG} missing {BASE_PREFAB}");
                return false;
            }

            // Built in a preview scene: the open scene is never touched (nor made dirty)
            Scene stage = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(source, stage);
                root.name = "Regax_Elite";
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                EnemyCharacterCoreInstaller core = root.GetComponent<EnemyCharacterCoreInstaller>();
                MonoEntity entity = root.GetComponent<MonoEntity>();
                if (core == null || entity == null)
                {
                    Debug.LogError($"{LOG} {BASE_PREFAB} has no EnemyCharacterCoreInstaller or MonoEntity on its root");
                    return false;
                }

                var coreSettings = new SerializedObject(core);
                coreSettings.FindProperty("_health").intValue = HEALTH;
                coreSettings.ApplyModifiedPropertiesWithoutUndo();

                if (root.TryGetComponent(out Rigidbody body))
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                }

                AICharacter character = root.AddComponent<AICharacter>();
                var characterSettings = new SerializedObject(character);
                characterSettings.FindProperty("_simulation").enumValueIndex = (int)AICharacter.SimulationMode.Manual;
                characterSettings.FindProperty("_body").managedReferenceValue = new EliteRegaxBody();
                characterSettings.FindProperty("_graph").objectReferenceValue = brain;
                characterSettings.ApplyModifiedPropertiesWithoutUndo();

                EliteRegaxInstaller installer = root.AddComponent<EliteRegaxInstaller>();
                var installerSettings = new SerializedObject(installer);
                installerSettings.FindProperty("_character").objectReferenceValue = character;
                installerSettings.FindProperty("_senses").objectReferenceValue = senses;
                installerSettings.ApplyModifiedPropertiesWithoutUndo();

                AtomicHitReceiver receiver = root.AddComponent<AtomicHitReceiver>();
                var receiverSettings = new SerializedObject(receiver);
                receiverSettings.FindProperty("_entity").objectReferenceValue = entity;
                receiverSettings.ApplyModifiedPropertiesWithoutUndo();

                ListInstaller(entity, installer);

                PrefabUtility.SaveAsPrefabAsset(root, PREFAB, out bool saved);
                if (!saved)
                    Debug.LogError($"{LOG} could not save {PREFAB}");
                return saved;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        /// <summary>Appends the installer to the entity's installers (after the Regax's own: their values come first).</summary>
        private static void ListInstaller(MonoEntity entity, MonoEntityInstaller installer)
        {
            var serialized = new SerializedObject(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == installer)
                    return;
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindTank()
        {
            VoxelSandboxStart start = Object.FindFirstObjectByType<VoxelSandboxStart>();
            if (start != null && start.Vehicle != null)
                return start.Vehicle.transform;
            GameObject tank = GameObject.Find("Tank_01");
            return tank != null ? tank.transform : null;
        }
    }
}
