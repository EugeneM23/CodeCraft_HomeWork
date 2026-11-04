using Atomic.Elements;
using Atomic.Entities;

namespace Game.Sandbox
{
    /// <summary>
    /// Refills the entity's health whenever it takes damage, so a target never runs out.
    ///
    /// Restoring rather than giving the target a huge pool keeps the whole damage path
    /// exercised: health really is reduced and DamageTakenEvent really fires before the refill,
    /// so hit reactions behave exactly as they do on a real character.
    /// </summary>
    public sealed class RestoreHealthOnDamageBehaviour : IEntityInit, IEntityDispose
    {
        private Health _health;
        private ISignal<TakeDamageArgs> _damageEvent;

        public void Init(IEntity entity)
        {
            _health = entity.GetHealth();
            _damageEvent = entity.GetDamageTakenEvent();
            _damageEvent.Subscribe(this.OnDamageTaken);
        }

        public void Dispose(IEntity entity) => _damageEvent.Unsubscribe(this.OnDamageTaken);

        private void OnDamageTaken(TakeDamageArgs args) => _health.SetCurrent(_health.GetMax());
    }
}
