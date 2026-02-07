using Unity.Entities;
using Unity.Transforms;

public static class SpawnUseCase
{

    public static void Prefab(EntityCommandBuffer ecb, RefRW<SpawnPrefabRequest> request, Entity entity)
    {
        var instance = ecb.Instantiate(request.ValueRW.Prefab);

        ecb.SetComponent(instance,
            LocalTransform.FromPositionRotation(request.ValueRW.Position, request.ValueRW.Rotaion));

        ecb.RemoveComponent<SpawnPrefabRequest>(entity);
    }
}