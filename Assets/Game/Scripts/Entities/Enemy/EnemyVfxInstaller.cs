using Atomic.Entities;
using UnityEngine;

namespace Game
{
    public sealed class EnemyVfxInstaller : MonoEntityInstaller
    {
        [SerializeField] private ParticleSystem _bulletBlood;

        [SerializeField] private ParticleSystem _meleeBlood;

        [SerializeField] private Transform _groundPoint;

        [SerializeField] private Transform _rootTransform;

        /// <summary>
        /// The effect to switch on when the character dies, disabled on the prefab. It is a
        /// GameObject rather than a ParticleSystem because the volumetric blood is a mesh driven
        /// by its own scripts, and because enabling the object is all the effect needs.
        /// </summary>
        [SerializeField] private GameObject _deathEffect;

        public override void Install(IEntity entity)
        {
            entity.AddBehaviour(new TakeDamageBloodBehaviour(_bulletBlood, _meleeBlood, _rootTransform));

            // The two ParticleSystem fields that used to stand here, _deadBlood and
            // _deathBloodVfx, were serialized and wired on Zombie.prefab but never read by
            // Install, so no character has ever shown a death effect. They are replaced by the
            // one field that is used.
            if (_deathEffect != null)
                entity.AddBehaviour(new DeathEffectBehaviour(_deathEffect));
        }
    }
}