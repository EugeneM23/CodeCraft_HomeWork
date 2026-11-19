using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Puts armour on the lab's target tank: the default armour layout asset (made once, then tuned by hand), the
    /// TankArmour component on TankDummy_01 and the armour stand (TankArmourLab) in the tank destruction lab.
    /// Safe to run again: an existing layout is kept as it is.
    /// </summary>
    public static class TankArmourSetup
    {
        public const string CONFIG = "Assets/Game/Configs/Tank/TankDummyArmourConfig.asset";
        private const string PREFAB = "Assets/Game/Resources/TankDummy_01.prefab";
        private const string SCENE = "Assets/Game/Scenes/Labs/TankDestructionLab.unity";

        [MenuItem("Tools/Combat/Set Up Tank Armour")]
        public static void SetUp()
        {
            TankArmourConfig config = Config();
            AddToPrefab(config);
            AddToLab();
            AssetDatabase.SaveAssets();
            Debug.Log("[Armour] set up: " + CONFIG);
        }

        /// <summary>Puts the default zones back into the layout asset (the hand tuning is lost).</summary>
        [MenuItem("Tools/Combat/Reset Tank Armour Layout")]
        public static void ResetLayout()
        {
            TankArmourConfig config = Config();
            Fill(config);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[Armour] layout reset: " + CONFIG);
        }

        /// <summary>The layout asset; made with the default zones when there is none.</summary>
        public static TankArmourConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<TankArmourConfig>(CONFIG);
            if (config != null)
                return config;
            config = ScriptableObject.CreateInstance<TankArmourConfig>();
            Fill(config);
            AssetDatabase.CreateAsset(config, CONFIG);
            return config;
        }

        /// <summary>
        /// The target tank's default layout: a heavy tank with a thick, near-upright front, a weaker lower front plate,
        /// sides that hold at an angle, thin roof and belly, a very thick gun mantlet and tracks as spaced armour.
        /// </summary>
        public static void Fill(TankArmourConfig config)
        {
            const ArmourFacing sides = ArmourFacing.Left | ArmourFacing.Right;
            TankArmourConfig.Zone Z(string name, string part, ArmourFacing facing, float mm, ArmourKind kind = ArmourKind.Plate,
                float yMin = 0f, float yMax = 1f, float zMin = 0f, float zMax = 1f) => new()
            {
                name = name, part = part, facing = facing, thickness = mm, kind = kind,
                boxMin = new Vector3(0f, yMin, zMin), boxMax = new Vector3(1f, yMax, zMax)
            };

            config.Set(ArmourRules.Default, new[]
            {
                // Inside: crew and modules (no armour of their own).
                Z("Экипаж", "Crewman", ArmourFacing.Any, 0f, ArmourKind.Module),
                Z("Двигатель", "Inside", ArmourFacing.Any, 0f, ArmourKind.Module, zMax: 0.3f),
                Z("Органы управления", "Inside", ArmourFacing.Any, 0f, ArmourKind.Module, zMin: 0.8f),
                Z("Боевое отделение", "Inside", ArmourFacing.Any, 0f, ArmourKind.Module),

                Z("Маска орудия", "Gun", ArmourFacing.Any, 150f),

                Z("Лоб башни", "TurretVisual", ArmourFacing.Front, 120f),
                Z("Крыша башни", "TurretVisual", ArmourFacing.Top | ArmourFacing.Bottom, 25f),
                Z("Борт башни", "TurretVisual", sides, 80f),
                Z("Корма башни", "TurretVisual", ArmourFacing.Rear, 80f),

                Z("Гусеница", "Track?_*", ArmourFacing.Any, 20f, ArmourKind.Spaced),
                Z("Каток", "Wheel_*", ArmourFacing.Any, 20f, ArmourKind.Spaced),

                Z("НЛД", "HullVisual", ArmourFacing.Front, 70f, yMax: 0.5f),
                Z("ВЛД", "HullVisual", ArmourFacing.Front, 110f),
                // The deck between the nose and the driver's plate: steep from the front, a ricochet trap.
                Z("Лобовой скос", "HullVisual", ArmourFacing.Top, 60f, zMin: 0.84f),
                Z("Крыша корпуса", "HullVisual", ArmourFacing.Top, 25f),
                Z("Днище", "HullVisual", ArmourFacing.Bottom, 25f),
                Z("Нижний борт", "HullVisual", sides, 60f, yMax: 0.5f),
                Z("Борт корпуса", "HullVisual", sides, 80f),
                Z("Корма корпуса", "HullVisual", ArmourFacing.Rear, 70f)
            }, 40f);
        }

        private static void AddToPrefab(TankArmourConfig config)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PREFAB);
            try
            {
                var armour = root.GetComponent<TankArmour>();
                if (armour == null)
                    armour = root.AddComponent<TankArmour>();
                armour.Config = config;
                PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddToLab()
        {
            var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
            TankLab lab = Object.FindFirstObjectByType<TankLab>();
            if (lab == null)
            {
                Debug.LogError("[Armour] no TankLab in " + SCENE);
                return;
            }

            AddStand(lab);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>The armour stand next to a TankLab, on the lab's camera.</summary>
        public static void AddStand(TankLab lab)
        {
            var stand = lab.GetComponent<TankArmourLab>();
            if (stand == null)
                stand = lab.gameObject.AddComponent<TankArmourLab>();
            var so = new SerializedObject(lab);
            stand.Camera = so.FindProperty("_camera").objectReferenceValue as Camera;
            EditorUtility.SetDirty(stand);
        }
    }
}
