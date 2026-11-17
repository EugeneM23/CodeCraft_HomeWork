using Atomic.Entities;

namespace Game.AI
{
    /// <summary>
    /// Keeps the level's grid (<see cref="LevelNav"/>) going: builds it at the start if nobody asked yet, takes in the
    /// path search of the last frame and starts the next, and schedules rays over the cells dug since the last frame
    /// (their heights come in at the start of the next frame, <see cref="LevelAIEarlyUpdate"/>).
    /// </summary>
    public sealed class LevelNavBehaviour : IEntityInit, IEntityTick, IEntityDispose
    {
        private LevelNav _nav;

        public void Init(IEntity entity)
        {
            _nav = entity.GetLevelNavigation();
            _nav.EnsureBuilt();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            _nav.PumpPaths();
            _nav.ScheduleUpdate();
        }

        public void Dispose(IEntity entity) => _nav?.Dispose();
    }
}
