using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Drops every buff on the entity when it dies.
    ///
    /// It used to listen to OnHealthChanged, which Health raises for any damage and any heal, so a
    /// single point of chip damage wiped the buffs the player had just earned. And with no Dispose
    /// the handler stayed on the Health instance, keeping a pooled corpse alive and still running
    /// DiscardAll on it.
    ///
    /// It listens to the death event, not to OnHealthEmpty. DeathBehaviour handles OnHealthEmpty
    /// too and on every character prefab it is installed first: it disposed the entity before this
    /// handler ran, the disposal cleared this behaviour, and the buffs were never dropped — the
    /// weapon kept ExtraDamageBuff's bonus and the buff particles stayed on. DeathBehaviour raises
    /// the death event before it disposes, so the order of the installers no longer matters.
    /// </summary>
    public class DiscardAllBuffBehaviour : IEntityInit, IEntityDispose
    {
        private IEntity _character;
        private IEvent<TakeDamageArgs> _deathEvent;

        public void Init(IEntity entity)
        {
            _character = entity;
            _deathEvent = entity.GetDeathTakenEvent();

            _deathEvent.Subscribe(this.DiscardAllBuffs);
        }

        public void Dispose(IEntity entity)
        {
            _deathEvent?.Unsubscribe(this.DiscardAllBuffs);

            _deathEvent = null;
            _character = null;
        }

        private void DiscardAllBuffs(TakeDamageArgs _)
        {
            if (_character != null)
                BuffUseCase.DiscardAll(_character);
        }
    }
}
