using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

public class UnitSpawner : MonoBehaviour
{
    [SerializeField] private Transform[] _spawnPoints;

    private float timer;

    public void SpawnKnight()
    {
        var spawnPosition = _spawnPoints[Random.Range(0, _spawnPoints.Length)].position;
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = World.DefaultGameObjectInjectionWorld.EntityManager;

        var catalog = manager.CreateEntityQuery(typeof(UnitEntityCatalog)).GetSingleton<UnitEntityCatalog>();
        var entity = ecb.CreateEntity();

        ecb.AddComponent(entity, new SpawnPrefabRequest
        {
            Position = spawnPosition,
            UnitPrefab = catalog.KnightBlue
        });

        ecb.Playback(manager);
        ecb.Dispose();
    }

    public void SpawnNecromancer()
    {
        var spawnPosition = _spawnPoints[Random.Range(0, _spawnPoints.Length)].position;

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var catalog = manager.CreateEntityQuery(typeof(UnitEntityCatalog)).GetSingleton<UnitEntityCatalog>();

        var entity = ecb.CreateEntity();
        ecb.AddComponent(entity, new SpawnPrefabRequest
        {
            Position = spawnPosition,
            UnitPrefab = catalog.NecromancerBlue
        });

        ecb.Playback(manager);
        ecb.Dispose();
    }
}