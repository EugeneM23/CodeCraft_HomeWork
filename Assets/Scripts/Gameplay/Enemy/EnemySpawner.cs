using System.Collections;
using Modules.Pools;
using UnityEngine;

namespace Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Enemy[] _enemies;
        [SerializeField] private Transform _spawnPoints;
        [SerializeField] private EnemyPool _enemyPool;
        [SerializeField] private BulletPool _bulletPool;
        [SerializeField] private WeaponData testData;

        [SerializeField] private int _maxEnemies = 10;
        [SerializeField] private float _maxSpawnTime = 3;
        [SerializeField] private float _minSpawnTime = 0;

        private IEnumerator Start()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minSpawnTime, _maxSpawnTime));

                if (_enemyPool.Pool.CountActive <= _maxEnemies)
                    SpawnEnemy();
            }
        }

        private void SpawnEnemy()
        {
            Enemy spawnEnemy = _enemyPool.Rent();
            int spawnIndex = Random.Range(0, _spawnPoints.childCount);
            spawnEnemy.transform.position = _spawnPoints.transform.GetChild(spawnIndex).position;
        }
    }
}