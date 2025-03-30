using System.Collections;
using Modules.Pools;
using UnityEngine;

namespace Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Enemy[] _enemies;
        [SerializeField] private Transform _spawnPoints;
        [SerializeField] private DefaultPool<Enemy> _enemyPool;

        private Transform _player;
        public BulletPool _bulletPool;

        private IEnumerator Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player").transform;

            while (true)
            {
                yield return new WaitForSeconds(Random.Range(0f, 1f));

                if (_enemyPool.Pool.CountActive <= _spawnPoints.childCount)
                    SpawnEnemy(_enemies[0]);
            }
        }

        private void SpawnEnemy(Enemy enemy)
        {
            Enemy spawnEnemy = _enemyPool.Rent();
            spawnEnemy.SetBulletPoolToGun(_bulletPool);
            int spawnIndex = Random.Range(0, _spawnPoints.childCount);

            spawnEnemy.transform.position = _spawnPoints.transform.GetChild(spawnIndex).position;
            spawnEnemy.SetTarget(_player);
        }
    }
}