using System.Collections;
using Modules.PrefabPool;
using UnityEngine;

namespace Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Character[] _enemies;
        [SerializeField] private Transform _spawnPoints;
        [SerializeField] private WeaponData testData;

        [SerializeField] private int _maxEnemies = 10;
        [SerializeField] private float _maxSpawnTime = 3;
        [SerializeField] private float _minSpawnTime = 0;

        private IEnumerator Start()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minSpawnTime, _maxSpawnTime));

                /*if (_enemyPool.Pool.CountActive <= _maxEnemies)
                    SpawnEnemy();*/
                
                SpawnEnemy();
            }
        }

        private void SpawnEnemy()
        {
            int randomIndex = Random.Range(0, _enemies.Length);
            Character spawnEnemy = PrefabPool.Instance.Spawn<Character>(_enemies[randomIndex].gameObject);
            int spawnIndex = Random.Range(0, _spawnPoints.childCount);
            spawnEnemy.transform.position = _spawnPoints.transform.GetChild(spawnIndex).position;
        }
    }
}