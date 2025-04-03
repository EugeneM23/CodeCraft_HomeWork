using System;
using System.Collections;
using Modules.PrefabPool;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Character[] _enemies;
        [SerializeField] private Transform _spawnPoints;
        [SerializeField] private Bulletmanager _bulletManager;

        [SerializeField] private int _maxEnemies = 10;
        [SerializeField] private float _maxSpawnTime = 3;
        [SerializeField] private float _minSpawnTime = 0;

        private PrefabPool _prefabPoll;

        private void Awake()
        {
            _prefabPoll = new PrefabPool();
        }

        private IEnumerator Start()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minSpawnTime, _maxSpawnTime));


                SpawnEnemy();
            }
        }

        private void SpawnEnemy()
        {
            int randomIndex = Random.Range(0, _enemies.Length);
            Character spawnEnemy = _prefabPoll.Spawn<Character>(_enemies[randomIndex].gameObject);
            spawnEnemy.SetBulletManager(_bulletManager);
            spawnEnemy.GetComponent<EnemyAI>().OnShoot += spawnEnemy.Shoot;

            int spawnIndex = Random.Range(0, _spawnPoints.childCount);
            spawnEnemy.transform.position = _spawnPoints.transform.GetChild(spawnIndex).position;
        }
    }
}