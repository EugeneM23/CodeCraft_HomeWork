using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Transforms;

public partial struct RotateToTargetWhileAttackingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach (var (transform, target) in SystemAPI
                     .Query<RefRW<LocalTransform>, RefRO<Target>>()
                     .WithAll<IsAttaking>())
        {
            if (target.ValueRO.Value == Entity.Null) continue;
            if (!transformLookup.HasComponent(target.ValueRO.Value)) continue;

            var targetPosition = transformLookup[target.ValueRO.Value].Position;
            RotateUseCase.RotateTowardsPosition(ref transform.ValueRW, targetPosition, 360f, deltaTime);
        }
    }
}