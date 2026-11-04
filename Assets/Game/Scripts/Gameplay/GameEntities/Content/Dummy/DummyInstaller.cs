using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game
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

            entity.AddHealth(new Health(_health, _health));

            // TakeDamageBloodBehaviour subscribes to both events, so the death one has to exist
            // even though nothing ever raises it here.
            entity.AddDamageTakenEvent(new Event<TakeDamageArgs>());
            entity.AddDeathTakenEvent(new Event<TakeDamageArgs>());

            entity.AddBehaviour<RestoreHealthOnDamageBehaviour>();

            if (_bulletBlood != null && _meleeBlood != null)
                entity.AddBehaviour(new TakeDamageBloodBehaviour(_bulletBlood, _meleeBlood, origin));
        }
    }
}
