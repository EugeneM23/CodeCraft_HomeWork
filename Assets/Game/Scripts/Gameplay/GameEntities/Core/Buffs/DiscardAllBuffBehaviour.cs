using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;

namespace Game
{
    /// <summary>
    /// Drops every buff on the entity when it dies.
    ///
    /// It used to listen to OnHealthChanged, which Health raises for any damage and any heal, so a
    /// single point of chip damage wiped the buffs the player had just earned. And with no Dispose
    /// the handler stayed on the Health instance, keeping a pooled corpse alive and still running
    /// DiscardAll on it.
    /// </summary>
    public class DiscardAllBuffBehaviour : IEntityInit, IEntityDispose
    {
        private IEntity _character;
        private Health _health;

        public void Init(IEntity entity)
        {
            _character = entity;
            _health = entity.GetHealth();

            _health.OnHealthEmpty += DiscardAllBuffs;
        }

        public void Dispose(IEntity entity)
        {
            if (_health != null)
                _health.OnHealthEmpty -= DiscardAllBuffs;

            _health = null;
            _character = null;
        }

        private void DiscardAllBuffs()
        {
            BuffUseCase.DiscardAll(_character);
        }
    }
}
