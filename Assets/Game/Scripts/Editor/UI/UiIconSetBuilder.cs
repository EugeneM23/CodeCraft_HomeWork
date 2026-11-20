using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Creates or updates Assets/Game/Configs/UI/UiIconSetConfig.asset: the UI pack's white icons by skill, slot and
    /// module id (tables below; an id not listed keeps its glyph). Idempotent: the asset keeps its GUID and gets the
    /// tables again. The prefab builders call Ensure and give the asset to the icon views.
    /// </summary>
    public static class UiIconSetBuilder
    {
        public const string PATH = UiDataBuilder.FOLDER + "/UiIconSetConfig.asset";

        // Skill id (SkillIds) → icon below Common/White Icons/Textures (or Icons000/)
        private static readonly string[,] Skills =
        {
            { "turret", "Icons1/ICON_Attack.png" },
            { "side_flamers", "Icons2/Icon_Fire.png" },
            { "mortar", "Icons2/Icon_Bullseye-arrow.png" },
            { "rockets", "Icons3/icon_Rocket.png" },
            { "rear_mines", "Icons2/Icon_Bomb.png" },
            { "air_strike", "Icons2/Icon_Plane.png" },
            { "ricochet", "Icons2/Icon_Navigation.png" },
            { "double_shot", "Icons2/Icon_Sword2.png" },
            { "piercing", "Icons2/Icon_Sword1.png" },
            { "big_caliber", "Icons2/Icon_Gun.png" },
            { "rate_of_fire", "Icons2/Icon_Bullet.png" },
            { "shrapnel", "Icons3/Icon_Snow.png" },
            { "high_explosive", "Icons4/Icon_Skull.png" },
            { "track_spikes", "Icons2/Icon_Boots.png" },
            { "armor", "Icons2/Icon_Shield.png" },
            { "regeneration", "Icons1/ICON_Health.png" },
            { "magnet", "Icons2/Icon_Magnet.png" },
            { "boost", "Icons2/Icon_Lightning.png" },
            { "shake", "Icons4/Icon_Warning.png" },
            { "luck", "Icons3/Icon_Dice.png" }
            // roller: no fitting icon, keeps its glyph
        };

        // Slot id (ModuleSlot name) → icon
        private static readonly string[,] Slots =
        {
            { nameof(ModuleSlot.Gun), "Icons2/Icon_Target.png" },
            { nameof(ModuleSlot.MachineGun), "Icons000/Icon_Gun1.png" },
            { nameof(ModuleSlot.Armor), "Icons2/Icon_Shield.png" },
            { nameof(ModuleSlot.Engine), "Icons2/Icon_Lightning.png" },
            { nameof(ModuleSlot.Tracks), "Icons2/Icon_Steeringwheel.png" },
            { nameof(ModuleSlot.Special), "Icons2/Icon_Magic2.png" }
        };

        // Module id → icon, where the module differs from its slot's icon
        private static readonly string[,] Modules =
        {
            { "gun_howitzer", "Icons2/Icon_Bullseye-arrow.png" },
            { "gun_88_he", "Icons2/Icon_Bomb.png" },
            { "armor_dozer", "Icons3/Icon_Tool.png" },
            { "engine_boosted", "Icons2/Icon_Fire.png" },
            { "special_smoke", "Icons3/Icon_Cloud.png" },
            { "special_repair", "Icons3/Icon_Wrench.png" },
            { "special_shockwave", "Icons3/Icon_Sun.png" }
        };

        [MenuItem("Tools/UI/Build Icon Set")]
        public static void Build() => Ensure();

        /// <summary>The icon set asset, created or refilled from the tables.</summary>
        public static UiIconSetConfig Ensure()
        {
            UiFactory.EnsureFolder(UiDataBuilder.FOLDER);
            UiIconSetConfig config = AssetDatabase.LoadAssetAtPath<UiIconSetConfig>(PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<UiIconSetConfig>();
                AssetDatabase.CreateAsset(config, PATH);
            }

            SerializedObject so = new(config);
            Fill(so.FindProperty("_skills"), Skills);
            Fill(so.FindProperty("_slots"), Slots);
            Fill(so.FindProperty("_modules"), Modules);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void Fill(SerializedProperty list, string[,] table)
        {
            int count = table.GetLength(0);
            list.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = table[i, 0];
                entry.FindPropertyRelative("sprite").objectReferenceValue = UiPackSprites.WhiteIcon(table[i, 1]);
            }
        }
    }
}
