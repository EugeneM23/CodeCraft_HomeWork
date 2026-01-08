using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 15f;
        [SerializeField] private float _rotationSpeed = 15f;

        [Header("Health Settings")]
        [SerializeField] private int _health = 100;

        [Header("Combat Settings")]
        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private Transform _weaponRoot;

        [Header("Core Components")]
        [SerializeField] private TriggerEventReceiver _triggerReceiver;
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private Transform _cameraPoint;

        [Header("Interaction")]
        [SerializeField] private InteractInstaller _interactInstaller;

        public override void Install(IEntity entity)
        {
            // Core
            entity.AddDamageableTag();
            entity.AddPlayerTag();
            entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddRiggedBody(_rigidbody);
            entity.AddVelocity(new ReactiveFloat());

            // Health
            entity.AddHealth(new Health(_health, _health));
            entity.AddDamageTakenEvent(new BaseEvent<TakeDamageArgs>());
            entity.AddDeathTakenEvent(new BaseEvent<TakeDamageArgs>());
            entity.AddBehaviour<DeathBehaviour>();

            // Movement
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddMoveSpeed(new ReactiveFloat(_moveSpeed));
            entity.AddMoveCondition(new AndExpression(
                entity.GetHealth().Exists,
                () => entity.GetWeapon().Value.GetMoveCondition().Invoke()
            ));
            entity.AddMoveDirection(new ReactiveVariable<Vector3>());
            entity.AddRotateDirection(new ReactiveVariable<Vector3>());
            entity.AddBehaviour<CharacterMoveBehaviour>();
            entity.AddBehaviour<CharacterRotateBehaviour>();
            entity.AddBehaviour<CharacterVelocityBehaviour>();

            // Combat
            entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon)); 
            entity.AddHandWeapon(new ReactiveVariable<IEntity>(_weapon));
            entity.AddWeaponRoot(_weaponRoot);
            entity.AddFireCondition(new AndExpression(entity.GetHealth().Exists));
            entity.AddFireAction(new CharacterFireAction(entity));
            entity.AddBehaviour(new DamageCastBehaviour());

            // Camera
            entity.AddCameraPoint(_cameraPoint);

            // Interaction
            _interactInstaller.Install(entity);
        }
    }
}
