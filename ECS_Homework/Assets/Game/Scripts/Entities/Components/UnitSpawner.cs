using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class UnitSpawner : MonoBehaviour
{
    [SerializeField] private Transform[] _spawnPoints;

    public void SpawnUnit()
    {
        var spawnPosition = _spawnPoints[Random.Range(0, _spawnPoints.Length)].position;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var catalog = manager.CreateEntityQuery(typeof(UnitEntityCatalog)).GetSingleton<UnitEntityCatalog>();

        var entity = ecb.CreateEntity();
        ecb.AddComponent(entity, new SpawnUnitRequest
        {
            Position = spawnPosition,
            UnitPrefab = catalog.KnightBlue
        });

        ecb.Playback(manager);
        ecb.Dispose();
    }
}