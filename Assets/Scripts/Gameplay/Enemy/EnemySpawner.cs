using System.Collections;
using UnityEngine;

namespace Gameplay
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private EnemyManager _manager;
        [SerializeField] private float _minSpawnTime;
        [SerializeField] private float _maxSpawnTime;

        private IEnumerator Start()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_minSpawnTime, _maxSpawnTime));
                _manager.SpawnEnemy();
            }
        }
    }
}