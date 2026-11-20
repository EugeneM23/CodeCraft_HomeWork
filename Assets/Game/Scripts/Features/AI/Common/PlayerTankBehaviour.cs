using Atomic.Entities;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Reads the player's vehicle once a frame for every agent of the level (<see cref="PlayerTank.Refresh"/>), before
    /// the AI world steps them: added to the level's AI entity ahead of AIKit's world behaviour.
    /// </summary>
    public sealed class PlayerTankBehaviour : IEntityInit, IEntityTick
    {
        private PlayerTank _tank;

        public void Init(IEntity entity) => _tank = entity.GetPlayerVehicle();

        public void Tick(IEntity entity, float deltaTime) => _tank.Refresh();
    }
}
