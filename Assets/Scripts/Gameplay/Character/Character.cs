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

        public void Shoot(Vector3 direction)
        {
            _weapon.Shoot(direction);
        }

        public void Move(Vector3 direction)
        {
            _moveComponent.Move(direction);
        }

        public void TakeDamage(int damage)
        {
            _healthComponent.TakeDamage(damage,this);
        }

        public void SetWeaponData(WeaponData testData)
        {
            _weapon.SetWeaponData(testData);
        }

        public void SetBulletManagerToWeapon(Bulletmanager bulletManager)
        {
            _weapon.SetBulletManager(bulletManager);
        }

        public void Destroy() => DeSpawn?.Invoke(gameObject);

        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }
    }
}