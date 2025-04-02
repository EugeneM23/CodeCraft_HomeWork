using System;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor.Connect;
using UnityEngine;

namespace Gameplay
{
    public class Character : MonoBehaviour, IDamageable
    {
        public event Action OnDath;
        [SerializeField] protected int _health;
        [SerializeField] private MoveComponent _moveComponent;
        [SerializeField] protected Weapon _weapon;

        public void Shoot()
        {
            _weapon.Shoot(Vector3.up);
        }

        public void Move(Vector3 direction)
        {
            _moveComponent.Move(direction);
        }

        public void TakeDamage(int damage)
        {
            _health -= damage;
            if (_health <= 0)
                OnDath?.Invoke();
        }

        public void SetWeaponData(WeaponData testData) => _weapon.SetWeaponData(testData);
    }
}