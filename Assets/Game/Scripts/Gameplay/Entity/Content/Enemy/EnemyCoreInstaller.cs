using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class EnemyCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private int _damage = 10;
        [SerializeField] private int _health = 100;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private TriggerEventReceiver _triggerReceiver;
        [SerializeField] private Transform _weaponRoot;
        [SerializeField] private Transform _characterRoot;

        [SerializeField] private Transform[] _patrolPoints;

        public override void Install(IEntity entity)
        {
            // 🧩 Core
            entity.AddDamage(new Const<int>(_damage));
            entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddDamageableTag();
            entity.AddPatrolPoints(_patrolPoints);
            entity.AddTarget(new ReactiveVariable<IEntity>());

            // ❤️ Health
            entity.AddHealth(new Health(_health, _health));

            // 🌀 Movement
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddMoveCondition(new AndExpression(entity.GetHealth().Exists));
            entity.AddMoveDirection(new ReactiveVariable<Vector3>());

            // ⚔️ Combat
            entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
            entity.AddWeaponRoot(_weaponRoot);

            entity.AddFireCondition(new AndExpression(entity.GetHealth().Exists));
            entity.AddFireEvent(new BaseEvent());
            entity.AddFireAction(new CharacterFireAction(entity));


            // ⚙️ Behaviours  
            entity.AddBehaviour<DeathBehaviour>();
            entity.AddBehaviour<CharacterRotateBehaviour>();
            entity.AddBehaviour<CharacterMoveAnimBehaviour>();
            entity.AddBehaviour<EnemyPatrolBehaviour>();
            entity.AddBehaviour<EnemyAttackBehaviour>();
            entity.AddBehaviour<EnemyChaseBehaviour>();
        }
    }
}