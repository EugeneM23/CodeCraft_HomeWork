using Atomic.Entities;
using UnityEngine;

namespace AIKit.Entities
{
    /// <summary>
    /// Makes an entity the owner of an AIKit world (one per level): the world (its decision budget, roster and global
    /// blackboard), the list of agents it steps and the behaviour that steps them (<see cref="AIWorldBehaviour"/>).
    /// Agents join it from their <see cref="AIAgentInstaller"/>. A game whose level entity has more to install calls
    /// <see cref="Install(IEntity, IAIWorld)"/> from its own installer instead of adding this component.
    /// </summary>
    public sealed class AIWorldInstaller : MonoEntityInstaller
    {
        [SerializeField, Tooltip("Seed of the world: agents with seed 0 take theirs from it, so a level replays.")]
        private int _seed = 1;

        [SerializeField, Min(0), Tooltip("At most this many agents decide in one step (0 — no limit); the rest decide on the next.")]
        private int _maxDecisionsPerStep = 8;

        public override void Install(IEntity entity)
        {
            var world = new AIWorld(_seed);
            world.Scheduler.MaxDecisionsPerStep = _maxDecisionsPerStep;
            Install(entity, world);
        }

        /// <summary>The world's values and its step on <paramref name="entity"/>; returns the agent list.</summary>
        public static AIAgentList Install(IEntity entity, IAIWorld world)
        {
            var agents = new AIAgentList(world);
            entity.AddAIWorldOwnerTag();
            entity.AddWorldAgentList(agents);
            entity.AddBehaviour(new AIWorldBehaviour());
            return agents;
        }
    }
}
