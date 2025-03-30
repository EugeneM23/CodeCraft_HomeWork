using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.Connect;
using UnityEngine;

namespace Gameplay
{
    public abstract class Character : MonoBehaviour, IDamageable
    {
        [SerializeField] protected int _health;
        [SerializeField] private MoveComponent _moveComponent;
        [SerializeField] protected Weapon _weapon;

        protected abstract void OnDeath();
        public virtual void Shoot() => _weapon.Shoot(Vector3.up);

        public virtual void Move(Vector3 direction) => _moveComponent.Move(direction);

        public virtual void TakeDamage(int damage)
        {
            _health -= damage;
            if (_health <= 0)
                OnDeath();
        }

        public void SetBulletPoolToWeapon(BulletPool pool) => _weapon.SetPool(pool);

        public void SetWeaponData(WeaponData testData) => _weapon.SetWeaponData(testData);
    }
}