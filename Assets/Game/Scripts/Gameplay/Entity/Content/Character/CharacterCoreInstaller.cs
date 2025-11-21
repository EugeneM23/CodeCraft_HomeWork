using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _moveSpeed = 15f;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private int _health = 100;
        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private TriggerEventReceiver _triggerReceiver;
        [SerializeField] private Transform _weaponRoot;
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private InteractInstaller _interactInstaller;

        public override void Install(IEntity entity)
        {
            // 🧩 Core
            entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddDamageableTag();
            entity.AddPlayerTag();

            // ❤️ Health
            entity.AddHealth(new Health(_health, _health));

            // 🌀 Movement
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddMoveSpeed(new Const<float>(_moveSpeed));

            entity.AddMoveCondition(new AndExpression(entity.GetHealth().Exists,
                () => entity.GetWeapon().Value.GetMoveCondition().Invoke()));

            entity.AddMoveDirection(new ReactiveVariable<Vector3>());

            // ⚔️ Combat
            entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
            entity.AddWeaponRoot(_weaponRoot);
            entity.AddFireCondition(new AndExpression(entity.GetHealth().Exists));
            entity.AddFireAction(new CharacterFireAction(entity));

            // ⚙️ Behaviours  
            entity.AddBehaviour<DeathBehaviour>();
            entity.AddBehaviour<CharacterMoveBehaviour>();
            entity.AddBehaviour<CharacterRotateBehaviour>();

            // 🛠️ Interact
            _interactInstaller.Install(entity);
        }
    }
}