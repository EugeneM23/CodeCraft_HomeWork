using Atomic.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds Regax_Mount, a Regax the player climbs on and rides, out of the enemy Regax.prefab,
    /// and parks one in the Playground. The enemy prefab itself is not touched.
    ///
    /// Everything visual and audible comes across as it is — skeleton, animator, blood, sounds, the
    /// claws as a nested Regax_Hand instance. What changes:
    ///   EnemyCharacterCoreInstaller  → MountInstaller (no AI; a vehicle to the player context)
    ///   layer Enemy         → Character, as the tank and helicopter: enemies' claws and the
    ///                         player's interaction probe both look there
    ///   Rigidbody           → rotation frozen like the player's, undamped, interpolated; it is driven through the body
    ///   claws               → hit the Enemy layer, harder and further, from in front of the chest
    ///   &lt;PatrolPoints&gt;      → gone
    ///   + CameraPoint, ExitPoints (Left, Right, Back), the pickups' highlight on the body
    ///
    /// Re-running rebuilds the prefab and puts the Playground mount back where it started.
    /// </summary>
    public static class RegaxMountBuilder
    {
        private const string LOG = "[REGAX MOUNT]";

        private const string SOURCE_PREFAB = "Assets/Game/Prefabs/Characters/Regax.prefab";
        private const string TARGET_PREFAB = "Assets/Game/Prefabs/Vehicles/Regax_Mount.prefab";
        private const string PLAYGROUND_SCENE = "Assets/Game/Scenes/Playground.unity";
        private const string NAME = "Regax_Mount";

        private const int LAYER_CHARACTER = 9;
        private const int LAYER_ENEMY = 8;

        /// <summary>
        /// The claws' damage sphere. On the enemy it sits at the feet with a 0.9 m reach, which is
        /// enough to catch a person standing against it; a rider swings at zombies a body-length
        /// ahead, so the sphere goes in front of the chest and grows.
        /// </summary>
        private static readonly Vector3 ClawsPosition = new(0f, 1f, 1.3f);
        private const float CLAWS_RADIUS = 1.4f;
        private const int CLAWS_DAMAGE = 50;

        /// <summary>The player's own camera point sits a metre ahead of the feet; the same here.</summary>
        private static readonly Vector3 CameraPointPosition = new(0f, 0f, 1f);

        private static readonly (string name, Vector3 position)[] ExitPoints =
        {
            ("Left", new Vector3(-1.4f, 0f, 0f)),
            ("Right", new Vector3(1.4f, 0f, 0f)),
            ("Back", new Vector3(0f, 0f, -1.6f)),
        };

        /// <summary>On open floor east of the player's start, clear of the tank, the helicopter and the aspect zones; facing the player.</summary>
        private static readonly Vector3 PlaygroundPosition = new(5f, 0f, -7f);

        [MenuItem("Tools/Regax/Build Mount")]
        public static void Build()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_PREFAB);

            if (source == null)
            {
                Debug.LogError($"{LOG} missing {SOURCE_PREFAB}");
                return;
            }

            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(source);

            // OutermostRoot keeps the claws a nested Regax_Hand instance; their changes are overrides.
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = NAME;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            if (!Convert(root))
            {
                Object.DestroyImmediate(root);
                return;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TARGET_PREFAB);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"{LOG} built {TARGET_PREFAB}");

            if (prefab != null)
                HookUpPlayground(prefab);
        }

        private static bool Convert(GameObject root)
        {
            EnemyCharacterCoreInstaller enemy = root.GetComponent<EnemyCharacterCoreInstaller>();
            MonoEntity entity = root.GetComponent<MonoEntity>();
            Rigidbody body = root.GetComponent<Rigidbody>();
            Transform skeleton = root.transform.Find("<Skeleton>");
            Transform weapons = root.transform.Find("<Weapons>");

            if (enemy == null || entity == null || body == null || skeleton == null || weapons == null)
            {
                Debug.LogError($"{LOG} {SOURCE_PREFAB} is missing EnemyCharacterCoreInstaller, MonoEntity, Rigidbody, <Skeleton> or <Weapons>");
                return false;
            }

            MonoEntity claws = weapons.GetComponentInChildren<MonoEntity>(true);
            MeleeWeaponInstaller clawsInstaller = claws != null ? claws.GetComponent<MeleeWeaponInstaller>() : null;

            if (clawsInstaller == null)
            {
                Debug.LogError($"{LOG} no melee weapon under <Weapons>");
                return false;
            }

            Transform patrol = root.transform.Find("<PatrolPoints>");
            if (patrol != null)
                Object.DestroyImmediate(patrol.gameObject);

            SetLayerRecursively(root, LAYER_CHARACTER);

            // Driven like the player: upright, turned and moved through the body, never by the solver.
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

            // The enemy's body carries a damping of 10 000 so nothing can shove it; it is moved by
            // transform and never notices. RigidbodyMoveBehaviour sets a velocity, and that damping
            // ate all of it inside the same physics step: the mount walked on the spot.
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;

            // The body moves in 50 Hz physics steps while the camera follows it every rendered
            // frame. Without interpolation, at ~100 fps every other frame showed it standing still
            // and the next one jumping twice as far: a visible shake while riding. The tank and the
            // helicopter interpolate for the same reason; RigidbodyRotateBehaviour turns it through
            // MoveRotation, and ControllableUseCase places the rider on the body, both safe with it.
            body.interpolation = RigidbodyInterpolation.Interpolate;

            weapons.localPosition = ClawsPosition;

            SerializedObject clawsSettings = new(clawsInstaller);
            clawsSettings.FindProperty("_damageLayer").intValue = 1 << LAYER_ENEMY;
            clawsSettings.FindProperty("_damageRadius").floatValue = CLAWS_RADIUS;
            clawsSettings.FindProperty("_damage").intValue = CLAWS_DAMAGE;
            clawsSettings.ApplyModifiedPropertiesWithoutUndo();

            Transform cameraPoint = Child(root.transform, "<CameraPoint>", CameraPointPosition);

            Transform exits = Child(root.transform, "<ExitPoints>", Vector3.zero);
            Transform[] exitPoints = new Transform[ExitPoints.Length];
            for (int i = 0; i < ExitPoints.Length; i++)
                exitPoints[i] = Child(exits, ExitPoints[i].name, ExitPoints[i].position);

            HighlightPlus.HighlightEffect highlight = TankBuilder.AddHighlight(skeleton);

            // The enemy installer goes, the mount installer takes its slot in the entity's list.
            Object.DestroyImmediate(enemy);
            MountInstaller mount = root.AddComponent<MountInstaller>();

            SerializedObject mountSettings = new(mount);
            mountSettings.FindProperty("_weapon").objectReferenceValue = claws;
            mountSettings.FindProperty("_weaponRoot").objectReferenceValue = weapons;
            mountSettings.FindProperty("_characterRoot").objectReferenceValue = root.transform;
            mountSettings.FindProperty("_rigidbody").objectReferenceValue = body;
            mountSettings.FindProperty("_cameraPoint").objectReferenceValue = cameraPoint;
            mountSettings.FindProperty("_highlight").objectReferenceValue = highlight;

            SerializedProperty exitsProperty = mountSettings.FindProperty("_exitPoints");
            exitsProperty.arraySize = exitPoints.Length;
            for (int i = 0; i < exitPoints.Length; i++)
                exitsProperty.GetArrayElementAtIndex(i).objectReferenceValue = exitPoints[i];
            mountSettings.ApplyModifiedPropertiesWithoutUndo();

            ReplaceInstaller(entity, mount);
            return true;
        }

        /// <summary>
        /// Puts the mount installer where the destroyed enemy one was (a null slot now), or first
        /// if there is no such slot: it adds the core values the other installers' behaviours read.
        /// </summary>
        private static void ReplaceInstaller(MonoEntity entity, MountInstaller mount)
        {
            SerializedObject serialized = new(entity);
            SerializedProperty list = serialized.FindProperty("sceneInstallers");

            int slot = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    slot = i;
                    break;
                }
            }

            if (slot < 0)
            {
                list.InsertArrayElementAtIndex(0);
                slot = 0;
            }

            list.GetArrayElementAtIndex(slot).objectReferenceValue = mount;

            // Any other empty slot would make the entity skip or throw on install.
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    list.DeleteArrayElementAtIndex(i);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            Transform child = new GameObject(name) { layer = parent.gameObject.layer }.transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;

            foreach (Transform child in target.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private static void HookUpPlayground(GameObject prefab)
        {
            Scene scene = SceneManager.GetActiveScene();

            if (scene.path != PLAYGROUND_SCENE)
            {
                Debug.LogWarning($"{LOG} Playground is not the active scene; the scene was left alone.");
                return;
            }

            bool hadUnsavedEdits = scene.isDirty;

            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.StartsWith(NAME))
                    Object.DestroyImmediate(root);

            GameObject mount = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            mount.name = NAME;
            mount.transform.SetPositionAndRotation(PlaygroundPosition, Quaternion.Euler(0f, -90f, 0f));

            EditorSceneManager.MarkSceneDirty(scene);

            if (hadUnsavedEdits)
                Debug.LogWarning($"{LOG} Playground had unsaved edits before the build, so it was not saved: " +
                                 "the mount is in the open scene, save it when those edits are ready.");
            else
                EditorSceneManager.SaveScene(scene);
        }
    }
}
