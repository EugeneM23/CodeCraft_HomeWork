using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public sealed class EnemyCoreInstaller : MonoEntityInstaller
    {
        [Header("Combat Settings")] [SerializeField]
        private int _damage = 10;

        [SerializeField] private MonoEntity _weapon;
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
            // AddValue now throws on null, which would abort the rest of Install.
            if (_triggerReceiver != null)
                entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddPatrolPoints(_patrolPoints);
            entity.AddTarget(new ReactiveVariable<IEntity>());

            // Health
            entity.AddHealth(new Health(_health, _health));
            entity.AddDeathTakenEvent(new Atomic.Elements.Event<TakeDamageArgs>());
            entity.AddDamageTakenEvent(new Atomic.Elements.Event<TakeDamageArgs>());
            entity.AddDeathEvent(new Atomic.Elements.Event());
            entity.AddBehaviour(new DeathBehaviour(_gameContext));

            // Movement
            entity.AddRotationSpeed(new Variable<float>(_rotationSpeed));
            entity.AddMoveCondition(new AndExpression(
                new ExpressionMember<bool>(entity.GetHealth().Exists),
                new ExpressionMember<bool>(() => entity.GetWeapon().Value.GetMoveCondition().Invoke())
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
            entity.AddFireCondition(new AndExpression(new ExpressionMember<bool>(entity.GetHealth().Exists)));
            entity.AddFireEvent(new Atomic.Elements.Event());
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