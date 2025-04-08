using Modules.Pooling;
using UnityEngine;

namespace Gameplay
{
    public class EnemyManager : MonoBehaviour
    {
        private const string Player = "Player";

        [SerializeField] private PointService _leveSpawnPoints;
        [SerializeField] private PointService _levelAttackPoints;
        [SerializeField] private EnemyCatalog _enemyCatalog;
        [SerializeField] private BulletManager _bulletManager;
        [SerializeField] private PrefabPool _prefabPoll;
        [SerializeField] private PlayerCharacterProvider _characterProvider;

        public void SpawnEnemy()
        {
            Transform player = _characterProvider.Character.gameObject.transform;
            Character spawnEnemy = _prefabPoll.Spawn<Character>(_enemyCatalog.GetEnemy());

            spawnEnemy.SetBulletManager(_bulletManager);
            spawnEnemy.GetComponent<EnemyAI>().SetDestination(_levelAttackPoints.NextPoint());
            spawnEnemy.GetComponent<EnemyAI>().SetAttackTarget(player);
            spawnEnemy.SetPosition(_leveSpawnPoints.NextPoint());

            spawnEnemy.DeSpawn += RemoveEnemy;
        }

        private void RemoveEnemy(GameObject enemy)
        {
            enemy.GetComponent<IDespawned>().DeSpawn -= RemoveEnemy;
            _prefabPoll.DeSpawn(enemy);
        }
    }
}