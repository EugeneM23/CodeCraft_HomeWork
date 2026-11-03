using UnityEngine;

namespace Game.PlaygroundTools
{
    /// <summary>
    /// Everything about where the playground puts things. Kept apart from the generator so the
    /// layout can be re-read and adjusted without going through the build code.
    /// </summary>
    internal static class PlaygroundLayout
    {
        public const string SourceScenePath = "Assets/Game/Scenes/GameScene.unity";
        public const string TargetScenePath = "Assets/Game/Scenes/Playground.unity";

        /// <summary>Root that holds everything the generator adds, so a rebuild can clear it.</summary>
        public const string PlaygroundRootName = "Playground";

        public const float FloorHalfSize = 40f;
        public const float WallHeight = 3f;

        /// <summary>
        /// Where the playground sits in the world. Lifted well clear of the old level, because a
        /// level root that holds any gameplay component stays switched on — the generator will not
        /// risk breaking a reference to it — and the two must not occupy the same space.
        /// </summary>
        public static readonly Vector3 Origin = new(0f, 200f, 0f);

        /// <summary>Character spawn, relative to <see cref="Origin"/>.</summary>
        public static readonly Vector3 CharacterSpawn = new(0f, 1.5f, 0f);

        /// <summary>Material for the generated primitives, so they do not render as magenta under URP.</summary>
        public const string DevMaterialPath = "Assets/Game/Materials/dev_material_stone.mat";

        public const string EnemyPrefabPath = "Assets/Game/Prefabs/Characters/Enemy.prefab";

        public static readonly string[] WeaponPickUpPaths =
        {
            "Assets/Game/Prefabs/Weapons/AR_01_PickUp.prefab",
            "Assets/Game/Prefabs/Weapons/AR_02_PickUp.prefab",
            "Assets/Game/Prefabs/Weapons/PT_01_PickUp.prefab",
            "Assets/Game/Prefabs/Weapons/Knife_01_PickUp.prefab"
        };

        public static readonly string[] ItemPickUpPaths =
        {
            "Assets/Game/Prefabs/Items/AmmoItem.prefab",
            "Assets/Game/Prefabs/Items/HealthItem.prefab"
        };

        public static readonly string[] BuffConfigPaths =
        {
            "Assets/Game/Configs/Buffs/ExtraDamageBuffConfig.asset",
            "Assets/Game/Configs/Buffs/MoveSpeedBuffConfig.asset",
            "Assets/Game/Configs/Buffs/JumpBuffConfig.asset",
            "Assets/Game/Configs/Buffs/StunBuffConfig.asset"
        };

        public const string JumpAspectPath = "Assets/Game/Configs/Aspects/JumpAspect.asset";

        /// <summary>
        /// Aspects for the effect-area station. The last two are both the jump aspect on purpose:
        /// their areas overlap, which is the case the refcounted grant exists for.
        /// </summary>
        public static readonly string[] EffectAreaAspectPaths =
        {
            "Assets/Game/Configs/Aspects/MoveSpeedAspect.asset",
            "Assets/Game/Configs/Aspects/DamageAspect.asset",
            "Assets/Game/Configs/Aspects/AspectComposite.asset",
            JumpAspectPath,
            JumpAspectPath
        };

        public enum StationKind
        {
            Weapons,
            Enemies,
            JumpTerrain,
            Abilities,
            Buffs,
            EffectAreas,
            Pooling
        }

        public readonly struct Station
        {
            public readonly StationKind Kind;
            public readonly string Name;
            public readonly Vector3 Center;

            public Station(StationKind kind, string name, Vector3 center)
            {
                this.Kind = kind;
                this.Name = name;
                this.Center = center;
            }
        }

        public static readonly Station[] Stations =
        {
            new(StationKind.Weapons, "Weapons", new Vector3(-18f, 0f, 0f)),
            new(StationKind.Enemies, "Enemies", new Vector3(-10f, 0f, 16f)),
            new(StationKind.JumpTerrain, "Jump terrain", new Vector3(10f, 0f, 16f)),
            new(StationKind.Abilities, "Abilities", new Vector3(18f, 0f, 0f)),
            new(StationKind.Buffs, "Buffs", new Vector3(-10f, 0f, -16f)),
            new(StationKind.EffectAreas, "Effect areas", new Vector3(10f, 0f, -16f)),
            new(StationKind.Pooling, "Pooling", new Vector3(0f, 0f, -28f))
        };

        /// <summary>Teleport radius from TeleportAbilityConfig, drawn as a ring of posts.</summary>
        public const float TeleportRadius = 5f;

        public static readonly float[] JumpStepHeights = { 0.5f, 1.1f, 1.8f, 2.6f, 3.5f };
    }
}
