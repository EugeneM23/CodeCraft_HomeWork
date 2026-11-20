using AIKit;
using AIKit.Entities;
using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Makes a <see cref="MonoEntity"/> the level's AI: the shared navigation grid (<see cref="LevelNav"/>,
    /// <see cref="LevelNavBehaviour"/>), the AIKit world with its agents (AIKit.Atomic's <see cref="AIWorldInstaller"/>, at most
    /// <see cref="MAX_DECISIONS_PER_STEP"/> decisions a step) and the player's vehicle as the AI sees it. One per level;
    /// <see cref="LevelAI.Ensure"/> makes one when the scene has none.
    /// </summary>
    public sealed class LevelAIInstaller : MonoEntityInstaller
    {
        public const int MAX_DECISIONS_PER_STEP = 8;

        [SerializeField]
        private LevelNavOptions _navigation = new();

        [SerializeField, Tooltip("Seed of the AIKit world: agents with seed 0 take theirs from it, so a level replays.")]
        private int _seed = 1;

        [SerializeField, Tooltip("Waves of the run's arenas by number for the arena director (Assets/Game/Configs/AI/Arenas/ArenaWavesTable).")]
        private ArenaWavesTable _arenaWaves;

        public LevelNavOptions Navigation => _navigation;

        /// <summary>Sets the grid's options before <see cref="Install"/> (a level AI made from code).</summary>
        public void Configure(LevelNavOptions navigation, ArenaWavesTable arenaWaves = null)
        {
            _navigation = navigation ?? new LevelNavOptions();
            if (arenaWaves != null)
                _arenaWaves = arenaWaves;
        }

        public override void Install(IEntity entity)
        {
            entity.AddAILevelTag();
            entity.AddLevelNavigation(new LevelNav(_navigation));
            var world = new AIWorld(_seed);
            world.Scheduler.MaxDecisionsPerStep = MAX_DECISIONS_PER_STEP;
            entity.AddPlayerVehicle(new PlayerTank());
            entity.AddBehaviour(new LevelNavBehaviour());
            entity.AddBehaviour(new PlayerTankBehaviour());   // the vehicle read once, before the agents step
            AIWorldInstaller.Install(entity, world);           // AIKit.Atomic: the agent list and the world's step
            var director = new ArenaDirector(_arenaWaves);
            entity.AddLevelArenaDirector(director);
            entity.AddBehaviour(director);
            if (entity is MonoEntity mono)
                LevelAI.Register(mono);
        }

        private void OnDestroy()
        {
            if (TryGetComponent(out MonoEntity mono))
                LevelAI.Unregister(mono);
        }
    }
}
