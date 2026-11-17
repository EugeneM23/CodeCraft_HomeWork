using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Entity keys of the AI team (Docs/Rules/Project.md, "Ключи сущностей"). The extensions
    /// (<c>GetHordeAlive()</c>, <c>AddHordeKills(...)</c>...) come from the Atomic source generator.
    /// </summary>
    [EntityExtensionsAPI]
    public static partial class AIAPI
    {
        #region Horde

        /// <summary>The horde entity (one per level).</summary>
        public static readonly TagKey<IEntity> Horde = new(nameof(Horde));

        /// <summary>Live monsters.</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<int>> HordeAlive = new(nameof(HordeAlive));

        /// <summary>Monsters killed since the horde was made.</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<int>> HordeKills = new(nameof(HordeKills));

        /// <summary>How many monsters the horde keeps alive: the dead are replaced far from the tank (0: no respawn).</summary>
        public static readonly ValueKey<IEntity, IReactiveVariable<int>> HordePopulation = new(nameof(HordePopulation));

        /// <summary>A monster died at this point (rewards, sound, quests).</summary>
        public static readonly ValueKey<IEntity, IEvent<Vector3>> HordeMonsterKilled = new(nameof(HordeMonsterKilled));

        /// <summary>Horde sound (density and waves); <see cref="HordeAudioNull"/> until the audio team plugs theirs in.</summary>
        public static readonly ValueKey<IEntity, IHordeAudio> HordeAudio = new(nameof(HordeAudio));

        #endregion

        #region Level AI

        /// <summary>The level's AI entity (one per level): the shared navigation grid, the AIKit world and its agents.</summary>
        public static readonly TagKey<IEntity> AILevel = new(nameof(AILevel));

        /// <summary>The level's navigation grid and its path service, shared by the horde and the elites.</summary>
        public static readonly ValueKey<IEntity, LevelNav> LevelNavigation = new(nameof(LevelNavigation));

        /// <summary>The level's AIKit world: the decision budget, the roster and the global blackboard.</summary>
        public static readonly ValueKey<IEntity, AIKit.IAIWorld> AgentWorld = new(nameof(AgentWorld));

        /// <summary>The agents the level steps (Manual simulation), in a fixed order.</summary>
        public static readonly ValueKey<IEntity, AIAgentList> WorldAgents = new(nameof(WorldAgents));

        /// <summary>The player's vehicle as the AI sees it: its transform and its hull.</summary>
        public static readonly ValueKey<IEntity, PlayerTank> PlayerVehicle = new(nameof(PlayerVehicle));

        #endregion

        #region Elite Regax

        /// <summary>An elite Regax driven by AIKit.</summary>
        public static readonly TagKey<IEntity> EliteRegax = new(nameof(EliteRegax));

        #endregion
    }
}
