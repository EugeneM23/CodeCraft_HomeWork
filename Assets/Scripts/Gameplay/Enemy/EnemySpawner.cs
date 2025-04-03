using System.Collections;
using Modules.PrefabPool;
using UnityEngine;
using UnityEngine.Serialization;

namespace Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private LevePointsManager _leveSpawnPoints;
        [SerializeField] private LevePointsManager _levelAttackPoints;

        [SerializeField] private Character[] _enemies;
        [SerializeField] private Bulletmanager _bulletManager;

        [SerializeField] private int _maxEnemies = 10;
        [SerializeField] private float _maxSpawnTime = 3;
        [SerializeField] private float _minSpawnTime = 0;

        private PrefabPool _prefabPoll;
        private Transform _player;

        private void Awake()
        {
            _prefabPoll = new PrefabPool();
        }

        private IEnumerator Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player").transform;
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

            SetupEnemy(spawnEnemy);
            SetSpawnPosition(spawnEnemy);
        }

        private void SetSpawnPosition(Character spawnEnemy)
        {
            spawnEnemy.SetPosition(_leveSpawnPoints.GetPoint());
        }

        private void SetupEnemy(Character spawnEnemy)
        {
            spawnEnemy.SetBulletManagerToWeapon(_bulletManager);
            spawnEnemy.GetComponent<EnemyAI>().SetAttackPointmanager(_levelAttackPoints);
            spawnEnemy.GetComponent<EnemyAI>().SetAttackTarget(_player);
        }
    }

    internal class LevelSpawnPointManager : MonoBehaviour
    {
    }
}