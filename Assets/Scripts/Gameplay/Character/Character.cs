using System;
using Modules.PrefabPool;
using UnityEngine;

namespace Gameplay
{
    public class Character : MonoBehaviour, IDamageable, IDespawned
    {
        public event Action<GameObject> DeSpawn;

        [SerializeField] private HealthComponent _healthComponent;
        [SerializeField] private MoveComponent _moveComponent;
        [SerializeField] private Weapon _weapon;

        private ImoveCondition _moveCondition;

        public void Move(Vector3 direction)
        {
            if (_moveCondition == null || _moveCondition.Invoke(transform.position + direction.normalized / 3))
                _moveComponent.Move(direction);
        }

        public void Shoot(Vector3 direction)
        {
            _weapon.Shoot(direction);
        }

        public void TakeDamage(int damage)
        {
            _healthComponent.TakeDamage(damage, this);
        }

        public void SetWeaponData(WeaponData wd)
        {
            _weapon.SetWeaponData(wd);
        }

        public void SetBulletManagerToWeapon(Bulletmanager bulletManager)
        {
            _weapon.SetBulletManager(bulletManager);
        }

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }

        public void Destroy()
        {
            DeSpawn?.Invoke(gameObject);
        }

        public void SetMoveOnScreeenCondition(ImoveCondition condition)
        {
            _moveCondition = condition;
        }
    }
}