using System;
using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public sealed class TowerInstaller : SceneEntityInstaller
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _rotationSpeed = 15f;
        [SerializeField] private int _health = 100;
        [SerializeField] private SceneEntity _weapon;

        public override void Install(IEntity entity)
        {
            entity.SetTarget(_target);
            entity.AddDamageableTag();
            entity.AddGameObject(transform.gameObject);
            entity.AddTransform(transform);
            entity.AddRotationSpeed(new BaseVariable<float>(_rotationSpeed));
            entity.AddHealth(new ReactiveInt(_health));

            entity.AddBehaviour<DeathBehaviour>();
            entity.AddBehaviour<LookAtBehaviour>();

            entity.AddWeapon(_weapon);
        }
    }
}