using System.Collections.Generic;
using Atomic.Entities;
using Game.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the Playground test scene from scratch.
    ///
    /// The scene is described here rather than authored by hand so it can be rebuilt with one
    /// call after any change, and so the diff in git is readable code instead of tens of
    /// thousands of lines of YAML.
    ///
    /// The rig ([Context], [Character], [CameraRoot]) is cloned from GameScene instead of being
    /// rebuilt: it carries dozens of wired serialized references — weapon catalog, pools, audio
    /// and hit prefabs, the entity world, the player systems — plus the tuned character physics.
    /// Re-wiring all of that from code would be both long and fragile. Everything the playground
    /// adds on top is built here.
    /// </summary>
    public static class PlaygroundBuilder
    {
        private const string SOURCE_SCENE = "Assets/Game/Scenes/GameScene.unity";
        private const string TARGET_SCENE = "Assets/Game/Scenes/Playground.unity";

        private const int LAYER_INTERACTIBLE = 6;
        private const int LAYER_DUMMY = 7;

        /// <summary>Roots of GameScene that are level content rather than rig.</summary>
        private static readonly string[] StripRoots = { "[Environment]", "[Level]" };

        private const float FLOOR_SIZE = 24f;
        private const float WALL_HEIGHT = 4f;
        private const float WALL_THICKNESS = 1f;
        private const float STATION_STEP = 2.5f;

        [MenuItem("Tools/Playground/Build")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(SOURCE_SCENE, OpenSceneMode.Single);

            StripLevel(scene);
            BuildBox();
            PlaceCharacter();

            Transform weapons = BuildWeapons();
            Transform buffs = BuildBuffPickups();
            Transform zones = BuildZones();
            Transform dummies = BuildDummies();
            BuildEnemies();

            Debug.Log($"[PGBUILD] weapons={weapons.childCount} buffs={buffs.childCount} " +
                      $"zones={zones.childCount} dummies={dummies.childCount}");

            bool saved = EditorSceneManager.SaveScene(scene, TARGET_SCENE);
            Debug.Log(saved
                ? $"[PGBUILD] saved {TARGET_SCENE}"
                : $"[PGBUILD] FAILED to save {TARGET_SCENE}");

            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ rig

        private static void StripLevel(Scene scene)
        {
            List<GameObject> doomed = new();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool isStripped = System.Array.IndexOf(StripRoots, root.name) >= 0;
                bool isLooseCube = root.name.StartsWith("Cube");

                if (isStripped || isLooseCube)
                    doomed.Add(root);
            }

            foreach (GameObject go in doomed)
                Object.DestroyImmediate(go);

            Debug.Log($"[PGBUILD] stripped {doomed.Count} level roots");
        }

        private static void PlaceCharacter()
        {
            GameObject character = GameObject.Find("[Character]");

            if (character == null)
            {
                Debug.LogError("[PGBUILD] no [Character] in the cloned scene");
                return;
            }

            GameObject cameraRoot = GameObject.Find("[CameraRoot]");

            // CameraSystemInstaller bakes the follow offset as cameraRoot.position minus
            // character.position at install time, so moving the character without moving the rig
            // changes the framing: the camera would sit wherever the old relative offset put it
            // and the player would be off screen. Capture the authored offset first, then restore
            // it around the new position.
            Vector3 framing = cameraRoot != null
                ? cameraRoot.transform.position - character.transform.position
                : Vector3.zero;

            // The capsule's centre sits at 1.08 with a height of 2.16, so the capsule bottom is
            // the pivot: y is the height of the feet. Slightly above the floor, not inside it.
            Vector3 spawn = new(0f, 0.05f, -8f);

            character.transform.position = spawn;
            character.transform.rotation = Quaternion.identity;

            if (cameraRoot == null)
            {
                Debug.LogWarning("[PGBUILD] no [CameraRoot]: camera framing left as it was");
                return;
            }

            cameraRoot.transform.position = spawn + framing;
            Debug.Log($"[PGBUILD] camera framing kept at {framing}, root at {cameraRoot.transform.position}");
        }

        // ------------------------------------------------------------------ box

        private static void BuildBox()
        {
            Transform box = new GameObject("<Box>").transform;

            float half = FLOOR_SIZE * 0.5f;
            float wallCentre = WALL_HEIGHT * 0.5f;

            // Floor top flush with y = 0.
            CreateCube("Floor", box, new Vector3(0f, -0.5f, 0f), new Vector3(FLOOR_SIZE, 1f, FLOOR_SIZE));

            CreateCube("Wall_N", box, new Vector3(0f, wallCentre, half),
                new Vector3(FLOOR_SIZE, WALL_HEIGHT, WALL_THICKNESS));
            CreateCube("Wall_S", box, new Vector3(0f, wallCentre, -half),
                new Vector3(FLOOR_SIZE, WALL_HEIGHT, WALL_THICKNESS));
            CreateCube("Wall_E", box, new Vector3(half, wallCentre, 0f),
                new Vector3(WALL_THICKNESS, WALL_HEIGHT, FLOOR_SIZE));
            CreateCube("Wall_W", box, new Vector3(-half, wallCentre, 0f),
                new Vector3(WALL_THICKNESS, WALL_HEIGHT, FLOOR_SIZE));
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;

            return cube;
        }

        // ------------------------------------------------------------------ stations

        private static Transform BuildWeapons()
        {
            Transform row = new GameObject("<Weapons>").transform;

            string[] paths =
            {
                "Assets/Game/Prefabs/Weapons/AR_01_PickUp.prefab",
                "Assets/Game/Prefabs/Weapons/AR_02_PickUp.prefab",
                "Assets/Game/Prefabs/Weapons/PT_01_PickUp.prefab",
                "Assets/Game/Prefabs/Weapons/Knife_01_PickUp.prefab",
                "Assets/Game/Prefabs/Items/AmmoItem.prefab",
                "Assets/Game/Prefabs/Items/HealthItem.prefab"
            };

            for (int i = 0; i < paths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);

                if (prefab == null)
                {
                    Debug.LogError($"[PGBUILD] missing prefab {paths[i]}");
                    continue;
                }

                GameObject instance = (GameObject) PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(row, false);
                instance.transform.position = ColumnPosition(i, paths.Length, -9f);
            }

            return row;
        }

        private static Transform BuildBuffPickups()
        {
            Transform row = new GameObject("<Buffs>").transform;

            string[] configs =
            {
                "Assets/Game/Configs/Buffs/ExtraDamageBuffConfig.asset",
                "Assets/Game/Configs/Buffs/JumpBuffConfig.asset",
                "Assets/Game/Configs/Buffs/MoveSpeedBuffConfig.asset",
                "Assets/Game/Configs/Buffs/StunBuffConfig.asset"
            };

            for (int i = 0; i < configs.Length; i++)
            {
                Object config = AssetDatabase.LoadAssetAtPath<Object>(configs[i]);

                if (config == null)
                {
                    Debug.LogError($"[PGBUILD] missing buff config {configs[i]}");
                    continue;
                }

                GameObject pickup = CreateCube(config.name, row, Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f));
                pickup.transform.position = ColumnPosition(i, configs.Length, 9f);

                // The interact query is an OverlapSphere masked to the Interactible layer, so a
                // pickup is only findable if it sits on that layer and carries a collider.
                pickup.layer = LAYER_INTERACTIBLE;

                MonoEntity entity = pickup.AddComponent<MonoEntity>();
                BuffPickUpInstaller installer = pickup.AddComponent<BuffPickUpInstaller>();

                SetPrivateField(installer, "buffConfig", config);
                AttachInstaller(entity, installer);
            }

            return row;
        }

        private static Transform BuildZones()
        {
            Transform row = new GameObject("<Zones>").transform;

            string[] aspects =
            {
                "Assets/Game/Configs/Aspects/DamageAspect.asset",
                "Assets/Game/Configs/Aspects/JumpAspect.asset",
                "Assets/Game/Configs/Aspects/MoveSpeedAspect.asset"
            };

            for (int i = 0; i < aspects.Length; i++)
            {
                Object aspect = AssetDatabase.LoadAssetAtPath<Object>(aspects[i]);

                if (aspect == null)
                {
                    Debug.LogError($"[PGBUILD] missing aspect {aspects[i]}");
                    continue;
                }

                GameObject zone = new(aspect.name);
                zone.transform.SetParent(row, false);
                // South of the spawn, behind the player: a zone in front of them would be crossed
                // by every shot.
                zone.transform.position = RowPosition(i, aspects.Length, -11f);

                BoxCollider collider = zone.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(2f, 2f, 2f);
                collider.center = new Vector3(0f, 1f, 0f);

                // Projectiles and dropped weapons have no business triggering an effect area, and
                // excluding them here means a stray shot does not even raise the event.
                collider.excludeLayers = (1 << 11) | (1 << 12) | (1 << 13);

                TriggerEventReceiver trigger = zone.AddComponent<TriggerEventReceiver>();
                MonoEntity entity = zone.AddComponent<MonoEntity>();
                EffectAreaInstaller installer = zone.AddComponent<EffectAreaInstaller>();

                SetPrivateField(installer, "_triggerEventReceiver", trigger);
                SetPrivateField(installer, "_entityAspect", aspect);
                AttachInstaller(entity, installer);
            }

            return row;
        }

        private static Transform BuildDummies()
        {
            Transform row = new GameObject("<Dummies>").transform;

            for (int i = 0; i < 3; i++)
            {
                GameObject dummy = CreateCube($"Dummy_{i + 1}", row, Vector3.zero, new Vector3(1f, 2f, 1f));
                // Straight ahead of the spawn: this is the firing lane, and nothing else is in it.
                dummy.transform.position = RowPosition(i, 3, 10f) + new Vector3(0f, 1f, 0f);
                dummy.layer = LAYER_DUMMY;

                MonoEntity entity = dummy.AddComponent<MonoEntity>();
                DummyInstaller installer = dummy.AddComponent<DummyInstaller>();

                AttachInstaller(entity, installer);
            }

            return row;
        }

        private static void BuildEnemies()
        {
            GameObject zombiePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Characters/Zombie.prefab");

            if (zombiePrefab == null)
            {
                Debug.LogError("[PGBUILD] missing Zombie prefab");
                return;
            }

            Transform enemies = new GameObject("<Enemies>").transform;
            GameObject standing = (GameObject) PrefabUtility.InstantiatePrefab(zombiePrefab);
            standing.transform.SetParent(enemies, false);
            standing.transform.position = new Vector3(6f, 0.05f, 6f);

            GameObject spawn = new("<Spawn>");
            spawn.transform.position = new Vector3(9f, 0.05f, 6f);

            GameObject triggers = new("<Triggers>");
            GameObject button = new("SpawnButton");
            button.transform.SetParent(triggers.transform, false);
            // South-west corner: clear of the firing lane and of the weapon column.
            button.transform.position = new Vector3(-9f, 0f, -11f);

            BoxCollider collider = button.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(2f, 2f, 2f);
            collider.center = new Vector3(0f, 1f, 0f);

            TriggerEventReceiver trigger = button.AddComponent<TriggerEventReceiver>();
            MonoEntity entity = button.AddComponent<MonoEntity>();
            SpawnButtonInstaller installer = button.AddComponent<SpawnButtonInstaller>();

            MonoEntity zombieEntity = zombiePrefab.GetComponent<MonoEntity>();

            if (zombieEntity == null)
                Debug.LogError("[PGBUILD] Zombie prefab has no MonoEntity");

            SetPrivateField(installer, "_trigger", trigger);
            SetPrivateField(installer, "_enemyPrefab", zombieEntity);
            SetPrivateField(installer, "_spawnPoint", spawn.transform);
            AttachInstaller(entity, installer);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A row spread along X at a fixed Z.</summary>
        private static Vector3 RowPosition(int index, int count, float z)
        {
            float width = (count - 1) * STATION_STEP;
            return new Vector3(index * STATION_STEP - width * 0.5f, 0.25f, z);
        }

        /// <summary>
        /// A column spread along Z at a fixed X. Stations live on the side walls so they are not
        /// in the firing line: the player spawns to the south and the targets stand to the north,
        /// and anything laid out between them gets shot through on every single shot.
        /// </summary>
        private static Vector3 ColumnPosition(int index, int count, float x)
        {
            float depth = (count - 1) * STATION_STEP;
            return new Vector3(x, 0.25f, index * STATION_STEP - depth * 0.5f);
        }

        /// <summary>
        /// Writes a private serialized field. Reflection would set the value without marking the
        /// object dirty, so it would never reach the saved scene.
        /// </summary>
        private static void SetPrivateField(Object target, string fieldName, Object value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogError($"[PGBUILD] no serialized field '{fieldName}' on {target.GetType().Name}");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Appends the installer to the entity's scene installer list.</summary>
        private static void AttachInstaller(MonoEntity entity, MonoEntityInstaller installer)
        {
            SerializedObject serialized = new(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");

            if (list == null)
            {
                Debug.LogError("[PGBUILD] MonoEntity has no 'sceneInstallers' field");
                return;
            }

            int index = list.arraySize;
            list.arraySize = index + 1;
            list.GetArrayElementAtIndex(index).objectReferenceValue = installer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
