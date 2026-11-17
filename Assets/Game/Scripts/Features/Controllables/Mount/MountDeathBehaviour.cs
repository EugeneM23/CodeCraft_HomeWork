using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// A mount that dies throws its rider off first, alive, with the player's controls
    /// and the enemies' attention handed back — the same hand-over a destroyed tank does.
    ///
    /// Has to be added before DeathBehaviour: that one disposes the entity on the same event, and
    /// a disposed mount no longer knows who rode it or where the exits are.
    /// </summary>
    public sealed class MountDeathBehaviour : IEntityInit, IEntityDispose
    {
        private readonly GameContext _gameContext;

        private IEntity _entity;
        private Health _health;

        public MountDeathBehaviour(GameContext gameContext) => _gameContext = gameContext;

        public void Init(IEntity entity)
        {
            _entity = entity;
            _health = entity.GetHealth();
            _health.OnHealthEmpty += OnDied;
        }

        public void Dispose(IEntity entity)
        {
            if (_health != null)
                _health.OnHealthEmpty -= OnDied;

            _health = null;
        }

        private void OnDied()
        {
            ControllableUseCase.Eject(_entity, _gameContext.GetPlayerContext().GetCharacter(),
                _gameContext.GetEntityWorld());

            // The body stays where it fell; nobody gets back on it.
            _entity.DelInteractableTag();
        }
    }
}
