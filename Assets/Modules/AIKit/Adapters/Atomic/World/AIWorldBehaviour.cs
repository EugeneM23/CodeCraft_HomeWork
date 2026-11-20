using Atomic.Entities;
using Unity.Profiling;

namespace AIKit.Entities
{
    /// <summary>
    /// The AI world's tick on its owner entity (one per level): opens the world's step and steps every agent once
    /// (<see cref="AIAgentList.Step"/>: <c>world.BeginStep(dt)</c>, then <c>agent.Step(dt)</c> each). Ticks through the
    /// Atomic lifecycle (<see cref="IEntityTick"/>), so the agents follow wherever the entity is ticked from — and stand
    /// still while it is paused. Whatever the game must refresh for every agent first (the player's vehicle) is a
    /// behaviour added before this one.
    /// </summary>
    public sealed class AIWorldBehaviour : IEntityInit, IEntityTick
    {
        private static readonly ProfilerMarker WorldMarker = new("AI.World");

        private AIAgentList _agents;

        public void Init(IEntity entity) => _agents = entity.GetWorldAgentList();

        public void Tick(IEntity entity, float deltaTime)
        {
            using (WorldMarker.Auto())
                _agents.Step(deltaTime);
        }
    }
}
