using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public sealed class EnemyCoreInstaller : SceneEntityInstaller
    {
        [Header("Combat Settings")] [SerializeField]
        private int _damage = 10;

        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private Transform _weaponRoot;

        [Header("Movement Settings")] [SerializeField]
        private float _moveSpeed = 1;

        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private Transform[] _patrolPoints;

        [Header("Health Settings")] [SerializeField]
        private int _health = 100;

        [Header("Core Components")] [SerializeField]
        private TriggerEventReceiver _triggerReceiver;

        [SerializeField] private Transform _characterRoot;

        private GameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;

            //Tags
            entity.AddDamageableTag();
            entity.AddEnemyTag();

            // Core
            entity.AddMoveSpeed(new ReactiveFloat(_moveSpeed));
            entity.AddDamage(new ReactiveInt(_damage));
            entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddPatrolPoints(_patrolPoints);
            entity.AddTarget(new ReactiveVariable<IEntity>());

            // Health
            entity.AddHealth(new Health(_health, _health));
            entity.AddDeathTakenEvent(new BaseEvent<TakeDamageArgs>());
            entity.AddDamageTakenEvent(new BaseEvent<TakeDamageArgs>());
            entity.AddDeathEvent(new BaseEvent());
            entity.AddBehaviour<DeathBehaviour>();

            // Movement
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddMoveCondition(new AndExpression(
                entity.GetHealth().Exists,
                () => entity.GetWeapon().Value.GetMoveCondition().Invoke()
            ));
            entity.AddMoveDirection(new ReactiveVariable<Vector3>());
            entity.AddRotateDirection(new ReactiveVariable<Vector3>());
            entity.AddVelocity(new ReactiveFloat());

            entity.AddBehaviour<CharacterMoveBehaviour>();
            entity.AddBehaviour<CharacterRotateBehaviour>();
            entity.AddBehaviour<CharacterVelocityBehaviour>();

            // Combat
            entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
            entity.AddWeaponRoot(_weaponRoot);
            entity.AddFireCondition(new AndExpression(entity.GetHealth().Exists));
            entity.AddFireEvent(new BaseEvent());
            entity.AddFireAction(new CharacterFireAction(entity));
            entity.AddBehaviour(new DamageCastBehaviour());

            // AI
            entity.AddBehaviour<EnemyPatrolBehaviour>();
            entity.AddBehaviour<EnemyChaseBehaviour>();
            entity.AddBehaviour<EnemyAttackBehaviour>();

            //Buffs
            entity.AddBuffsEffects(new ReactiveList<BuffBase>());
        }
    }
}