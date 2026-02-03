using ProjectDawn.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial struct SpawnPrefabSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        var manager = state.EntityManager;
        foreach (var (request, entity) in SystemAPI.Query<SpawnPrefabRequest>().WithEntityAccess())
        {
            var ins = ecb.Instantiate(request.Prefab);

            ecb.SetComponent(ins, new LocalTransform
            {
                Position = request.Position,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            ecb.AddComponent(ins, new NewUnitTag());

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(manager);
        ecb.Dispose();
    }
}