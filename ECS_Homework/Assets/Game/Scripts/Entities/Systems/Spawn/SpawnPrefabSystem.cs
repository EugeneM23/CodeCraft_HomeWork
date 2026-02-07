using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial struct SpawnPrefabSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (request, entity) in SystemAPI.Query<RefRW<SpawnPrefabRequest>>().WithEntityAccess())
            SpawnUseCase.Prefab(ecb, request, entity);

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}