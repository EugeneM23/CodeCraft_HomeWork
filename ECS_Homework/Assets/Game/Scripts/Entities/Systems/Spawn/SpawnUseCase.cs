using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class SpawnUseCase
{
    public static void Projectile(
        Target target,
        LocalTransform transform,
        LocalTransform targetTransform,
        EntityCommandBuffer ecb,
        ProjectilePrefab projectilePrefab
    )
    {
        var spawnPosition = transform.Position + new float3(0, 1f, 0);
        float3 direction = math.normalize(targetTransform.Position - spawnPosition);
        var spawnRotation = quaternion.LookRotationSafe(direction, math.up());

        var instantiate = ecb.Instantiate(projectilePrefab.Value);
        ecb.SetComponent(instantiate, LocalTransform.FromPositionRotation(spawnPosition, spawnRotation));
        ecb.AddComponent(instantiate, new Target { Value = target.Value });
    }

    public static void Prefab(EntityCommandBuffer ecb, RefRW<SpawnPrefabRequest> request, Entity entity)
    {
        var instance = ecb.Instantiate(request.ValueRW.Prefab);
        ecb.SetComponent(instance, LocalTransform.FromPosition(request.ValueRW.Position));
        ecb.RemoveComponent<SpawnPrefabRequest>(entity);
    }
}