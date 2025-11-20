using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class EnemyCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private int _health = 100;
        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private TriggerEventReceiver _triggerReceiver;
        [SerializeField] private Transform _weaponRoot;
        [SerializeField] private Transform _characterRoot;

        public override void Install(IEntity entity)
        {
            // 🧩 Core
            entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddDamageableTag();

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
            entity.AddFireAction(new CharacterFireAction(entity));

            // ⚙️ Behaviours  
            entity.AddBehaviour<DeathBehaviour>();
            entity.AddBehaviour<CharacterRotateBehaviour>();
            entity.AddBehaviour<EnemyMoveBehaviour>();
        }
    }
}