using Atomic.Entities;

namespace Game.AI
{
    /// <summary>
    /// The level's AIKit tick: reads the player's vehicle once for every agent, then steps the world and its agents
    /// (<see cref="AIAgentList.Step"/>: <c>world.BeginStep(dt)</c>, then <c>agent.Step(dt)</c> each). Ticks through the
    /// Atomic world (<see cref="IEntityTick"/>), so the AI follows wherever the entity world is ticked from.
    /// </summary>
    public sealed class AIWorldBehaviour : IEntityInit, IEntityTick
    {
        private AIAgentList _agents;
        private PlayerTank _tank;

        public void Init(IEntity entity)
        {
            _agents = entity.GetWorldAgents();
            _tank = entity.GetPlayerVehicle();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            _tank.Refresh();
            _agents.Step(deltaTime);
        }
    }
}
