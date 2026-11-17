using AIKit;
using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Makes a <see cref="MonoEntity"/> the level's AI: the shared navigation grid (<see cref="LevelNav"/>,
    /// <see cref="LevelNavBehaviour"/>), the AIKit world with its agents (<see cref="AIWorldBehaviour"/>, at most
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

        public LevelNavOptions Navigation => _navigation;

        /// <summary>Sets the grid's options before <see cref="Install"/> (a level AI made from code).</summary>
        public void Configure(LevelNavOptions navigation)
        {
            _navigation = navigation ?? new LevelNavOptions();
        }

        public override void Install(IEntity entity)
        {
            entity.AddAILevelTag();
            entity.AddLevelNavigation(new LevelNav(_navigation));
            var world = new AIWorld(_seed);
            world.Scheduler.MaxDecisionsPerStep = MAX_DECISIONS_PER_STEP;
            entity.AddWorldAgents(new AIAgentList(world));
            entity.AddPlayerVehicle(new PlayerTank());
            entity.AddBehaviour(new LevelNavBehaviour());
            entity.AddBehaviour(new AIWorldBehaviour());
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
