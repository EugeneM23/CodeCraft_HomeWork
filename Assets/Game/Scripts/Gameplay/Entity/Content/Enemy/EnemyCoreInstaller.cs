using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public sealed class EnemyCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _damage = 10;
        [SerializeField] private float _moveSpeed = 1;
        [SerializeField] private int _health = 100;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private TriggerEventReceiver _triggerReceiver;
        [SerializeField] private Transform _weaponRoot;
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Transform[] _patrolPoints;
        private GameContext _gameContext;

        public override void Install(IEntity entity)
        {
            _gameContext = GameContext.Instance;

            // 🧩 Core
            entity.AddMoveSpeed(new Const<float>(_moveSpeed));
            entity.AddDamage(new Const<int>(_damage));
            entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddDamageableTag();
            entity.AddPatrolPoints(_patrolPoints);
            entity.AddTarget(new ReactiveVariable<IEntity>());

            // ❤️ Health
            entity.AddHealth(new Health(_health, _health));
            entity.AddDeathTakenEvent(new BaseEvent<TakeDamageArgs>());
            entity.AddDamageTakenEvent(new BaseEvent<TakeDamageArgs>());
            entity.AddDeathEvent(new BaseEvent());

            // 🌀 Movement
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddMoveCondition(new AndExpression(entity.GetHealth().Exists,
                () => entity.GetWeapon().Value.GetMoveCondition().Invoke()));
            entity.AddMoveDirection(new ReactiveVariable<Vector3>());
            entity.AddRotateDirection(new ReactiveVariable<Vector3>());
            entity.AddVelocity(new ReactiveFloat());

            // ⚔️ Combat
            entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
            entity.AddWeaponRoot(_weaponRoot);
            entity.AddFireCondition(new AndExpression(entity.GetHealth().Exists));
            entity.AddFireEvent(new BaseEvent());
            entity.AddFireAction(new CharacterFireAction(entity));

            // ⚙️ Behaviours  
            entity.AddBehaviour<DeathBehaviour>();
            entity.AddBehaviour<CharacterRotateBehaviour>();
            entity.AddBehaviour<CharacterMoveBehaviour>();
            entity.AddBehaviour<CharacterVelocityBehaviour>();
            entity.AddBehaviour<EnemyPatrolBehaviour>();
            entity.AddBehaviour<EnemyAttackBehaviour>();
            entity.AddBehaviour<EnemyChaseBehaviour>();
            entity.AddBehaviour(new DamageCastBehaviour(_gameContext));
        }
    }
}