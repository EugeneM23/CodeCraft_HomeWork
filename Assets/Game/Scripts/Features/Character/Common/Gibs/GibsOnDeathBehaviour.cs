using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Breaks the character into its gibs when it dies: the body is hidden and a gibs prefab
    /// takes its place, in the same pose, thrown away from the killing hit.
    /// </summary>
    /// <remarks>See GibRules.ReadHit for how the killing hit is known.</remarks>
    public sealed class GibsOnDeathBehaviour : IEntityInit, IEntityDispose
    {
        private readonly Gibs _prefab;
        private readonly Transform _skeleton;
        private readonly Renderer[] _body;

        private IEvent<TakeDamageArgs> _damageTakenEvent;
        private IEvent<TakeDamageArgs> _deathTakenEvent;
        private TakeDamageArgs _lastHit;

        public GibsOnDeathBehaviour(Gibs prefab, Transform skeleton, Renderer[] body)
        {
            _prefab = prefab;
            _skeleton = skeleton;
            _body = body;
        }

        public void Init(IEntity entity)
        {
            _damageTakenEvent = entity.GetDamageTakenEvent();
            _deathTakenEvent = entity.GetDeathTakenEvent();

            _damageTakenEvent.Subscribe(this.OnDamageTaken);
            _deathTakenEvent.Subscribe(this.OnDeath);
        }

        public void Dispose(IEntity entity)
        {
            _damageTakenEvent.Unsubscribe(this.OnDamageTaken);
            _deathTakenEvent.Unsubscribe(this.OnDeath);
        }

        private void OnDamageTaken(TakeDamageArgs args) => _lastHit = args;

        private void OnDeath(TakeDamageArgs _)
        {
            foreach (Renderer renderer in _body)
                renderer.enabled = false;

            KillingHit hit = GibRules.ReadHit(_lastHit, _skeleton.position);
            Gibs gibs = Object.Instantiate(_prefab, _skeleton.position, _skeleton.rotation);
            gibs.Burst(_skeleton, hit.Origin, hit.Damage, hit.Blast, hit.BlastShare);
        }
    }
}
