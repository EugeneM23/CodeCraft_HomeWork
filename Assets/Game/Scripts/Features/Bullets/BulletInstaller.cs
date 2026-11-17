using Atomic.Elements;
using Atomic.Entities;
using RayFire;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public class BulletInstaller : MonoEntityInstaller
    {
        [SerializeField] private float _moveSpeed = 10f;
        [SerializeField] private float _lifeTime = 1f;
        [SerializeField] private MonoEntity _hitPrefab;
        [SerializeField] private CollisionEventReceiver _collisionEventReceiver;
        [Tooltip("RayFire impact settings this bullet applies to destructible objects. Optional.")]
        [SerializeField] private RayfireGun _destructionImpact;
        [Tooltip("Chance (0..1) that a hit knocks the brick it strikes out at once, whatever damage it already took.")]
        [Range(0f, 1f)]
        [SerializeField] private float _knockoutChance;
        [Header("Ricochet")]
        [Tooltip("How many times the bullet may bounce off surfaces it grazes. 0 turns ricochet off.")]
        [Min(0)]
        [SerializeField] private int _ricochetMaxCount;
        [Tooltip("Largest angle (degrees) between the flight direction and the surface that still bounces the bullet off.")]
        [Range(0f, 90f)]
        [SerializeField] private float _ricochetMaxAngle = 30f;
        [Tooltip("Share (0..1) of the RayFire impact a destructible surface takes when the bullet bounces off it.")]
        [Range(0f, 1f)]
        [SerializeField] private float _ricochetImpactFactor = 0.3f;
        private GameFactory _factory;

        public override void Install(IEntity entity)
        {
            _factory = GameContext.Instance.GetGameFactory();

            //Core
            entity.AddRangeWeaponTag();
            entity.AddEntityID(gameObject.name.Replace("(Clone)", ""));
            entity.AddBulletHitPrefab(_hitPrefab);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);

            //LifeTime
            entity.AddLifeTime(new CooldownTimer(_lifeTime));
            entity.AddBehaviour(new LifeTimeBehaviour());
            entity.AddDestroyAction(new InlineAction(() => _factory.Destroy(entity)));

            //Movement
            entity.AddMoveSpeed(new ReactiveFloat(_moveSpeed));
            entity.AddMoveDirection(new ReactiveVector3(transform.forward));

            //Collision
            entity.AddCollisionReceiver(_collisionEventReceiver);
            if (_destructionImpact != null)
                entity.AddDestructionImpact(_destructionImpact);
            if (_knockoutChance > 0f)
                entity.AddDestructionKnockoutChance(_knockoutChance);
            entity.AddBehaviour<BulletCollisionBehaviour>();

            // Swept: at 20 m/s a physics step is 0.4 m, longer than a brick wall is thick.
            Rigidbody body = GetComponent<Rigidbody>();
            entity.AddBehaviour(new BulletMoveBehaviour(body));

            //Ricochet
            if (_ricochetMaxCount > 0)
            {
                entity.AddRicochetMaxCount(_ricochetMaxCount);
                entity.AddRicochetMaxAngle(_ricochetMaxAngle);
                entity.AddRicochetImpactFactor(_ricochetImpactFactor);
                entity.AddRicochetCount(0);
                // The bounce zeroes the velocity the solver gives the body when it hits the wall.
                if (body != null)
                    entity.AddRiggedBody(body);
            }
        }
    }
}