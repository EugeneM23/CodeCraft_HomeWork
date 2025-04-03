using System;
using Modules.PrefabPool;
using UnityEngine;

namespace Gameplay
{
    public class Character : MonoBehaviour, IDamageable, IDespawned
    {
        public Action<GameObject> OnDespawn;
        
        [SerializeField] private HealthComponent _healthComponent;
        [SerializeField] private MoveComponent _moveComponent;
        [SerializeField] private Weapon _weapon;

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
            _healthComponent.TakeDamage(damage, OnDespawn, this.gameObject);
        }

        public void SetWeaponData(WeaponData testData) => _weapon.SetWeaponData(testData);

        public void SetDespawnCallBack(Action<GameObject> callback)
        {
            OnDespawn = callback;
        }
    }
}