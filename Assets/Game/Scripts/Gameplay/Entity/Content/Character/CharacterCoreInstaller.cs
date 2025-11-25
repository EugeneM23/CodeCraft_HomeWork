using System;
using System.Collections.Generic;
using Atomic.Elements;
using Atomic.Entities;
using Game.Gameplay;
using Modules.Gameplay;
using UnityEngine;

namespace Game
{
    public sealed class CharacterCoreInstaller : SceneEntityInstaller
    {
        [SerializeField] private float _moveSpeed = 15f;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private int _health = 100;
        [SerializeField] private SceneEntity _weapon;
        [SerializeField] private SceneEntity _handWeapon;
        [SerializeField] private TriggerEventReceiver _triggerReceiver;
        [SerializeField] private Transform _weaponRoot;
        [SerializeField] private Transform _characterRoot;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private InteractInstaller _interactInstaller;
        [SerializeField] private Transform _cameraPoint;

        public override void Install(IEntity entity)
        {
            // 🧩 Core
            entity.AddDamageableTag();
            entity.AddPlayerTag();
            entity.AddTriggerEventReceiver(_triggerReceiver);
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(_characterRoot);
            entity.AddRiggedBody(_rigidbody);
            entity.AddVelocity(new ReactiveFloat());

            // ❤️ Health
            entity.AddHealth(new Health(_health, _health));
            entity.AddDamageTakenEvent(new BaseEvent<TakeDamageArgs>());
            entity.AddDeathTakenEvent(new BaseEvent<TakeDamageArgs>());

            // 🌀 Movement
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddMoveSpeed(new Const<float>(_moveSpeed));
            entity.AddMoveCondition(new AndExpression(entity.GetHealth().Exists,
                () => entity.GetWeapon().Value.GetMoveCondition().Invoke()));

            entity.AddMoveDirection(new ReactiveVariable<Vector3>());
            entity.AddRotateDirection(new ReactiveVariable<Vector3>());

            // ⚔️ Combat
            entity.AddWeapon(new ReactiveVariable<IEntity>(_weapon));
            entity.AddHandWeapon(new ReactiveVariable<IEntity>(_weapon));
            entity.AddWeaponRoot(_weaponRoot);
            entity.AddFireCondition(new AndExpression(entity.GetHealth().Exists));
            entity.AddFireAction(new CharacterFireAction(entity));

            // ⚙️ Behaviours  
            entity.AddBehaviour<DeathBehaviour>();
            entity.AddBehaviour<CharacterMoveBehaviour>();
            entity.AddBehaviour<CharacterRotateBehaviour>();
            entity.AddBehaviour<CharacterVelocityBehaviour>();

            //Camera
            entity.AddCameraPoint(_cameraPoint);

            // 🛠️ Interact
            _interactInstaller.Install(entity);
        }
    }
}