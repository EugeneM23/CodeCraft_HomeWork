using System;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Scripts.Entities.Systems
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private float _spawnDelay = 1f;
        [SerializeField] private Transform[] _spawnPoints;
        private float _timer;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0)
            {
                _timer = _spawnDelay;
                SpawnKnight();
            }
        }

        private void SpawnKnight()
        {
            var spawnPosition = _spawnPoints[Random.Range(0, _spawnPoints.Length)].position;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var catalog = manager.CreateEntityQuery(typeof(UnitEntityCatalog)).GetSingleton<UnitEntityCatalog>();
            var entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new SpawnPrefabRequest
            {
                Position = spawnPosition,
                UnitPrefab = catalog.KnightRed
            });
            ecb.Playback(manager);
            ecb.Dispose();
        }
    }
}