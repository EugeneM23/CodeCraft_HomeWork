using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Sandbox
{
    /// <summary>
    /// A shooting target: takes damage and shows it, never dies, never fights back.
    ///
    /// It gets no DeathBehaviour and no AI, so it is not an enemy — it sits on the Dummy layer
    /// and stays out of the enemy filters. DamageUseCase only reduces health and raises
    /// DamageTakenEvent, so without a death behaviour the target is immortal by construction;
    /// the refill behaviour just keeps its health off zero so hits keep registering.
    /// </summary>
    public sealed class DummyInstaller : MonoEntityInstaller
    {
        [SerializeField] private int _health = 100;
        [SerializeField] private Transform _originTransform;
        [SerializeField] private ParticleSystem _bulletBlood;
        [SerializeField] private ParticleSystem _meleeBlood;

        public override void Install(IEntity entity)
        {
            Transform origin = _originTransform != null ? _originTransform : this.transform;

            entity.AddDamageableTag();
            entity.AddGameObject(this.gameObject);
            entity.AddTransform(origin);

            // The death event is never raised here (no DeathBehaviour), but TakeDamageBloodBehaviour
            // subscribes to it.
            HealthUseCase.AddHealthWithEvents(entity, _health);

            entity.AddBehaviour<RestoreHealthOnDamageBehaviour>();

            if (_bulletBlood != null && _meleeBlood != null)
                entity.AddBehaviour(new TakeDamageBloodBehaviour(_bulletBlood, _meleeBlood, origin));
        }
    }
}
