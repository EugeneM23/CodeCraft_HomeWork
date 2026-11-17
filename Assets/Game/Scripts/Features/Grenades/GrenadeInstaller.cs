using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A thrown grenade: a physics body that bounces and rolls, and explodes when its fuse runs out.
    /// Put ExplosionInstaller next to it for the blast itself. The thrower sets the grenade's
    /// velocity and lights the fuse (GrenadeThrowAction).
    /// </summary>
    public sealed class GrenadeInstaller : MonoEntityInstaller
    {
        [Tooltip("Damage at the centre of the blast; ExplosionInstaller's falloff scales it down to the edge.")]
        [SerializeField] private int _damage = 80;
        [Tooltip("Seconds from the throw to the explosion.")]
        [SerializeField] private float _fuse = 2f;
        [Tooltip("Linear damping from the first contact on, so the grenade stops near where it lands. None in flight.")]
        [SerializeField] private float _groundDamping = 5f;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private CollisionEventReceiver _collisionReceiver;

        public override void Install(IEntity entity)
        {
            Rigidbody body = _rigidbody != null ? _rigidbody : GetComponent<Rigidbody>();

            //Core
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddGameObject(gameObject);
            entity.AddTransform(transform);
            entity.AddRiggedBody(body);

            //Landing
            if (_collisionReceiver != null)
                entity.AddBehaviour(new GrenadeLandingBehaviour(body, _collisionReceiver, _groundDamping));

            //Damage
            entity.AddDamage(new ReactiveInt(_damage));

            //Fuse
            entity.AddGrenadeFuse(new CooldownTimer(_fuse));
            entity.AddBehaviour<GrenadeFuseBehaviour>();
        }
    }
}
