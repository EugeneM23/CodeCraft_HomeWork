using System;
using Modules.Pools;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class EnemyPool : DefaultPool<Enemy>
    {
        [SerializeField] private Weapon _enemyWeapon;
        [SerializeField] private BulletPool _bulletPool;

        private Transform _player;

        private void Start() => _player = GameObject.FindGameObjectWithTag("Player").transform;

        protected override void OnCreate(Enemy enemy)
        {
            enemy.SetBulletPoolToWeapon(_bulletPool);
            enemy.SetEnemyPull(this);
            enemy.SetTarget(_player);
        }
    }
}