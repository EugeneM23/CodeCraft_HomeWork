using System.Collections.Generic;
using System.Linq;
using Atomic.Entities;
using Game.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.PlaygroundTools
{
    /// <summary>
    /// Generates the playground scene by copying the working scene and taking it apart, rather
    /// than assembling the contexts from scratch.
    /// </summary>
    /// <remarks>
    /// GameContextInstaller and PlayerContextInstaller together carry dozens of private serialized
    /// references that are correct today. A copy inherits all of them. The cost is that this
    /// overwrites Playground.unity on every run, so hand edits to that scene do not survive.
    /// </remarks>
    internal static class PlaygroundBuilder
    {
        private const string MenuPath = "Tools/Playground/Rebuild Scene";

        private static Material _devMaterial;

        [MenuItem(MenuPath)]
        private static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog(
                    "Rebuild the playground?",
                    $"{PlaygroundLayout.TargetScenePath} will be overwritten from " +
                    $"{PlaygroundLayout.SourceScenePath}. Anything edited in the playground scene by " +
                    "hand is lost.",
                    "Rebuild", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            _devMaterial = AssetDatabase.LoadAssetAtPath<Material>(PlaygroundLayout.DevMaterialPath);

            Scene source = EditorSceneManager.OpenScene(PlaygroundLayout.SourceScenePath, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(source, PlaygroundLayout.TargetScenePath, true))
            {
                Debug.LogError("Playground: could not save the scene copy.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(PlaygroundLayout.TargetScenePath, OpenSceneMode.Single);

            DeactivateArt(scene);

            GameObject root = new GameObject(PlaygroundLayout.PlaygroundRootName);
            root.transform.position = PlaygroundLayout.Origin;
            BuildFloor(root.transform);

            foreach (PlaygroundLayout.Station station in PlaygroundLayout.Stations)
                BuildStation(root.transform, station);

            PlaceCharacter();
            AddOverlay(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"Playground: rebuilt {PlaygroundLayout.TargetScenePath}.");
        }

        #region Stripping

        /// <summary>
        /// Switches off every root that is pure decoration. Deactivation rather than deletion: a
        /// kept object may hold a reference into one of these hierarchies, and a missing reference
        /// is a far worse failure than a switched-off GameObject that is still in the hierarchy.
        /// </summary>
        private static void DeactivateArt(Scene scene)
        {
            var kept = new List<string>();
            var hidden = new List<string>();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == PlaygroundLayout.PlaygroundRootName)
                {
                    Object.DestroyImmediate(root);
                    continue;
                }

                if (IsGameplay(root))
                {
                    kept.Add(root.name);
                    continue;
                }

                root.SetActive(false);
                hidden.Add(root.name);
            }

            Debug.Log($"Playground: kept {kept.Count} roots ({string.Join(", ", kept)}); " +
                      $"switched off {hidden.Count} ({string.Join(", ", hidden)}).");
        }

        private static bool IsGameplay(GameObject root)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                    continue;

                if (component is Camera or Light or AudioListener or AudioSource)
                    return true;

                string assembly = component.GetType().Assembly.GetName().Name;
                if (assembly is "Game" or "Modules" or "Atomic.Entities" or "Atomic.Elements")
                    return true;
            }

            // GameContextInstaller points its pools parent at a plain transform.
            return root.name.Contains("Pool");
        }

        #endregion

        #region Geometry

        private static void BuildFloor(Transform parent)
        {
            const float half = PlaygroundLayout.FloorHalfSize;

            GameObject floor = Box(parent, "Floor", new Vector3(0f, -0.5f, 0f),
                new Vector3(half * 2f, 1f, half * 2f));
            floor.layer = GroundLayer();

            Box(parent, "Wall_North", new Vector3(0f, PlaygroundLayout.WallHeight * 0.5f, half),
                new Vector3(half * 2f, PlaygroundLayout.WallHeight, 1f)).layer = GroundLayer();
            Box(parent, "Wall_South", new Vector3(0f, PlaygroundLayout.WallHeight * 0.5f, -half),
                new Vector3(half * 2f, PlaygroundLayout.WallHeight, 1f)).layer = GroundLayer();
            Box(parent, "Wall_East", new Vector3(half, PlaygroundLayout.WallHeight * 0.5f, 0f),
                new Vector3(1f, PlaygroundLayout.WallHeight, half * 2f)).layer = GroundLayer();
            Box(parent, "Wall_West", new Vector3(-half, PlaygroundLayout.WallHeight * 0.5f, 0f),
                new Vector3(1f, PlaygroundLayout.WallHeight, half * 2f)).layer = GroundLayer();
        }

        private static int GroundLayer()
        {
            int layer = LayerMask.NameToLayer("Ground");
            return layer >= 0 ? layer : 0;
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 scale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = scale;

            if (_devMaterial != null)
                box.GetComponent<MeshRenderer>().sharedMaterial = _devMaterial;

            return box;
        }

        private static Transform Station(Transform parent, PlaygroundLayout.Station station)
        {
            GameObject go = new GameObject($"Station_{station.Kind}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = station.Center;

            // A low plinth, so you can see where a station begins without reading the hierarchy.
            Box(go.transform, "Plinth", new Vector3(0f, 0.05f, 0f), new Vector3(9f, 0.1f, 9f))
                .layer = GroundLayer();

            return go.transform;
        }

        #endregion

        #region Stations

        private static void BuildStation(Transform parent, PlaygroundLayout.Station station)
        {
            Transform root = Station(parent, station);

            switch (station.Kind)
            {
                case PlaygroundLayout.StationKind.Weapons:
                    BuildWeapons(root);
                    break;
                case PlaygroundLayout.StationKind.Enemies:
                    BuildEnemies(root);
                    break;
                case PlaygroundLayout.StationKind.JumpTerrain:
                    BuildJumpTerrain(root);
                    break;
                case PlaygroundLayout.StationKind.Abilities:
                    BuildAbilities(root);
                    break;
                case PlaygroundLayout.StationKind.Buffs:
                    BuildBuffs(root);
                    break;
                case PlaygroundLayout.StationKind.EffectAreas:
                    BuildEffectAreas(root);
                    break;
                case PlaygroundLayout.StationKind.Pooling:
                    BuildPooling(root);
                    break;
            }
        }

        private static void BuildWeapons(Transform root)
        {
            float x = -3f;

            foreach (string path in PlaygroundLayout.WeaponPickUpPaths)
            {
                SpawnPrefab(path, root, new Vector3(x, 1f, 0f));
                x += 2f;
            }

            x = -1f;
            foreach (string path in PlaygroundLayout.ItemPickUpPaths)
            {
                SpawnPrefab(path, root, new Vector3(x, 1f, 2.5f));
                x += 2f;
            }

            // Something to shoot and swing at, close enough to hear the impacts.
            for (int i = 0; i < 4; i++)
                Box(root, $"Target_{i}", new Vector3(-3f + i * 2f, 1f, -3.5f), Vector3.one);
        }

        private static void BuildEnemies(Transform root)
        {
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaygroundLayout.EnemyPrefabPath);
            if (enemyPrefab == null)
            {
                Debug.LogWarning($"Playground: no enemy prefab at {PlaygroundLayout.EnemyPrefabPath}.");
                return;
            }

            var enemies = new List<MonoEntity>();

            for (int i = 0; i < 2; i++)
            {
                Vector3 center = new Vector3(i == 0 ? -2.5f : 2.5f, 0f, 0f);

                GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                enemy.name = $"Enemy_{i}";
                enemy.transform.SetParent(root, false);
                enemy.transform.localPosition = center + Vector3.up;

                Transform[] points = BuildPatrolPoints(root, i, center);
                AssignPatrolPoints(enemy, points);

                // The entity component is not necessarily on the prefab root.
                MonoEntity entity = enemy.GetComponentInChildren<MonoEntity>(true);
                if (entity != null)
                    enemies.Add(entity);
            }

            BuildChaseTrigger(root, enemies);
        }

        private static Transform[] BuildPatrolPoints(Transform root, int index, Vector3 center)
        {
            var holder = new GameObject($"PatrolRoute_{index}");
            holder.transform.SetParent(root, false);
            holder.transform.localPosition = center;

            Vector3[] offsets =
            {
                new(-1.5f, 0f, -1.5f), new(1.5f, 0f, -1.5f),
                new(1.5f, 0f, 1.5f), new(-1.5f, 0f, 1.5f)
            };

            var points = new Transform[offsets.Length];

            for (int i = 0; i < offsets.Length; i++)
            {
                var point = new GameObject($"Point_{i}");
                point.transform.SetParent(holder.transform, false);
                point.transform.localPosition = offsets[i];
                points[i] = point.transform;
            }

            return points;
        }

        /// <summary>
        /// Enemy.prefab ships two unassigned patrol points, and a prefab cannot reference scene
        /// transforms — so this is the only way the patrol behaviour ever gets a real route.
        /// </summary>
        private static void AssignPatrolPoints(GameObject enemy, Transform[] points)
        {
            foreach (MonoEntityInstaller installer in enemy.GetComponentsInChildren<MonoEntityInstaller>(true))
            {
                var serialized = new SerializedObject(installer);
                SerializedProperty property = serialized.FindProperty("_patrolPoints");

                if (property == null || !property.isArray)
                    continue;

                property.arraySize = points.Length;
                for (int i = 0; i < points.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = points[i];

                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            Debug.LogWarning($"Playground: no _patrolPoints field found on {enemy.name}.");
        }

        private static void BuildChaseTrigger(Transform root, List<MonoEntity> enemies)
        {
            var trigger = new GameObject("ChaseTrigger");
            trigger.transform.SetParent(root, false);
            trigger.transform.localPosition = new Vector3(0f, 1.5f, -3f);

            BoxCollider collider = trigger.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(9f, 3f, 4f);

            EnemyTrigger enemyTrigger = trigger.AddComponent<EnemyTrigger>();

            var serialized = new SerializedObject(enemyTrigger);
            SerializedProperty property = serialized.FindProperty("_enemies");

            if (property == null || !property.isArray)
            {
                Debug.LogWarning("Playground: EnemyTrigger has no _enemies field.");
                return;
            }

            property.arraySize = enemies.Count;
            for (int i = 0; i < enemies.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildJumpTerrain(Transform root)
        {
            float z = -3.5f;

            foreach (float height in PlaygroundLayout.JumpStepHeights)
            {
                Box(root, $"Step_{height:0.0}", new Vector3(-2.5f, height * 0.5f, z),
                    new Vector3(2.5f, height, 1.5f)).layer = GroundLayer();
                z += 1.8f;
            }

            // A ramp, to see whether the grounded probe holds on a slope.
            GameObject ramp = Box(root, "Ramp", new Vector3(2.5f, 1f, 0f), new Vector3(3f, 0.3f, 8f));
            ramp.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            ramp.layer = GroundLayer();

            // A gap with nothing under it: the jump must not fire in mid-air.
            Box(root, "Ledge", new Vector3(0f, 1.5f, 4f), new Vector3(3f, 3f, 2f)).layer = GroundLayer();
        }

        private static void BuildAbilities(Transform root)
        {
            // A ring of posts at the teleport radius, so the ability edge is visible.
            const int posts = 16;
            for (int i = 0; i < posts; i++)
            {
                float angle = i * Mathf.PI * 2f / posts;
                var position = new Vector3(
                    Mathf.Cos(angle) * PlaygroundLayout.TeleportRadius,
                    0.5f,
                    Mathf.Sin(angle) * PlaygroundLayout.TeleportRadius);

                Box(root, $"TeleportRing_{i}", position, new Vector3(0.2f, 1f, 0.2f));
            }

            // Distance marks for the dash.
            for (int i = 1; i <= 4; i++)
                Box(root, $"DashMark_{i}", new Vector3(-4f + i * 2f, 0.15f, -3f),
                    new Vector3(0.1f, 0.3f, 2f));

            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaygroundLayout.EnemyPrefabPath);
            if (enemyPrefab == null)
                return;

            // A standing dummy for the stun: no patrol points, so it only ever reacts.
            GameObject dummy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            dummy.name = "StunDummy";
            dummy.transform.SetParent(root, false);
            dummy.transform.localPosition = new Vector3(0f, 1f, 3f);
            AssignPatrolPoints(dummy, new Transform[0]);
        }

        private static void BuildBuffs(Transform root)
        {
            BuffPickUpInstaller template = FindTemplate<BuffPickUpInstaller>();

            if (template == null)
            {
                Debug.LogWarning("Playground: no BuffPickUpInstaller in the scene to clone.");
                return;
            }

            float x = -3f;

            foreach (string path in PlaygroundLayout.BuffConfigPaths)
            {
                var config = AssetDatabase.LoadAssetAtPath<BuffConfig>(path);
                if (config == null)
                {
                    Debug.LogWarning($"Playground: no buff config at {path}.");
                    continue;
                }

                GameObject pickup = Object.Instantiate(EntityObjectOf(template), root);
                pickup.name = $"BuffPickUp_{config.name}";
                pickup.transform.localPosition = new Vector3(x, 1f, 0f);
                pickup.SetActive(true);

                foreach (BuffPickUpInstaller installer in pickup.GetComponentsInChildren<BuffPickUpInstaller>(true))
                    SetReference(installer, "buffConfig", config);

                x += 2f;
            }

            DashPickUpInstaller dash = FindTemplate<DashPickUpInstaller>();

            if (dash == null)
                return;

            GameObject dashPickup = Object.Instantiate(EntityObjectOf(dash), root);
            dashPickup.name = "DashPickUp";
            dashPickup.transform.localPosition = new Vector3(0f, 1f, 2.5f);
            dashPickup.SetActive(true);
        }

        private static void BuildEffectAreas(Transform root)
        {
            EffectAreaInstaller template = FindTemplate<EffectAreaInstaller>();

            if (template == null)
            {
                Debug.LogWarning("Playground: no EffectAreaInstaller in the scene to clone.");
                return;
            }

            string[] paths = PlaygroundLayout.EffectAreaAspectPaths;

            for (int i = 0; i < paths.Length; i++)
            {
                var aspect = AssetDatabase.LoadAssetAtPath<ScriptableEntityAspect>(paths[i]);
                if (aspect == null)
                {
                    Debug.LogWarning($"Playground: no aspect at {paths[i]}.");
                    continue;
                }

                GameObject area = Object.Instantiate(EntityObjectOf(template), root);
                area.SetActive(true);

                // The last two are both the jump aspect and sit on top of each other: that overlap
                // is the case the refcounted jump grant exists for.
                bool overlapping = i >= paths.Length - 2;

                area.name = overlapping ? $"EffectArea_Overlap_{i}" : $"EffectArea_{aspect.name}";
                area.transform.localPosition = overlapping
                    ? new Vector3(2.5f + (i - paths.Length + 2) * 1.2f, 0.5f, -3f)
                    : new Vector3(-3f + i * 3f, 0.5f, 2.5f);

                foreach (EffectAreaInstaller installer in area.GetComponentsInChildren<EffectAreaInstaller>(true))
                    SetReference(installer, "_entityAspect", aspect);
            }
        }

        private static void BuildPooling(Transform root)
        {
            for (int x = 0; x < 5; x++)
            for (int y = 0; y < 4; y++)
                Box(root, $"PoolTarget_{x}_{y}", new Vector3(-4f + x * 2f, 0.6f + y * 1.2f, -3.5f),
                    new Vector3(1.4f, 1f, 0.4f));
        }

        #endregion

        #region Wiring

        private static GameObject SpawnPrefab(string path, Transform parent, Vector3 localPosition)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Playground: no prefab at {path}.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            return instance;
        }

        /// <summary>
        /// The GameObject to clone for a scene-authored entity: the nearest MonoEntity at or above
        /// the installer. Cloning transform.root instead would drag in the whole level root the
        /// template happens to live under.
        /// </summary>
        private static GameObject EntityObjectOf(Component installer)
        {
            MonoEntity entity = installer.GetComponentInParent<MonoEntity>(true);
            return entity != null ? entity.gameObject : installer.gameObject;
        }

        private static bool IsUnderPlayground(Component component)
        {
            for (Transform t = component.transform; t != null; t = t.parent)
                if (t.name == PlaygroundLayout.PlaygroundRootName)
                    return true;

            return false;
        }

        private static T FindTemplate<T>() where T : Component
        {
            return Object
                .FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(instance => !IsUnderPlayground(instance));
        }

        private static void SetReference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Debug.LogWarning($"Playground: {target.GetType().Name} has no field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PlaceCharacter()
        {
            PlayerContext player = Object
                .FindObjectsByType<PlayerContext>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();

            if (player == null)
            {
                Debug.LogWarning("Playground: no PlayerContext in the scene.");
                return;
            }

            MonoEntity character = FindCharacter(player);
            if (character != null)
                character.transform.root.position = PlaygroundLayout.Origin + PlaygroundLayout.CharacterSpawn;

            PointEnemySpawnAtStation();
        }

        private static MonoEntity FindCharacter(PlayerContext player)
        {
            foreach (MonoEntityInstaller installer in player.GetComponentsInChildren<MonoEntityInstaller>(true))
            {
                var serialized = new SerializedObject(installer);
                SerializedProperty property = serialized.FindProperty("_character");

                if (property != null && property.objectReferenceValue is MonoEntity character)
                    return character;
            }

            return Object
                .FindObjectsByType<MonoEntity>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(entity => entity.HasPlayerTag());
        }

        /// <summary>Points the M-key spawn at the enemy station instead of the old level.</summary>
        private static void PointEnemySpawnAtStation()
        {
            GameObject station = GameObject.Find(
                $"{PlaygroundLayout.PlaygroundRootName}/Station_{PlaygroundLayout.StationKind.Enemies}");

            if (station == null)
                return;

            var spawn = new GameObject("EnemySpawnPoint");
            spawn.transform.SetParent(station.transform, false);
            spawn.transform.localPosition = new Vector3(0f, 1f, 3f);

            foreach (MonoEntityInstaller installer in Object
                         .FindObjectsByType<MonoEntityInstaller>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(installer);
                SerializedProperty property = serialized.FindProperty("_spawnPoint");

                if (property == null)
                    continue;

                property.objectReferenceValue = spawn.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        }

        private static void AddOverlay(Transform parent)
        {
            var go = new GameObject("PlaygroundOverlay");
            go.transform.SetParent(parent, false);

            PlaygroundOverlay overlay = go.AddComponent<PlaygroundOverlay>();

            GameContext game = Object
                .FindObjectsByType<GameContext>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();

            PlayerContext player = Object
                .FindObjectsByType<PlayerContext>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();

            string[] names = PlaygroundLayout.Stations.Select(station => station.Name).ToArray();

            // World space: the overlay compares them against the character transform.
            Vector3[] positions = PlaygroundLayout.Stations
                .Select(station => PlaygroundLayout.Origin + station.Center)
                .ToArray();

            overlay.Configure(game, player, names, positions);
            EditorUtility.SetDirty(overlay);
        }

        #endregion
    }
}
