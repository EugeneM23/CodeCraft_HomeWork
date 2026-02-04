using Game.Scripts.Entities.Systems;
using Unity.Entities;
using Unity.Transforms;

public partial struct ProjectileMoveSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        
        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach (var (transform, target, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<Target>, RefRO<Speed>>()
                     .WithAll<ProjectileTag>())
        {
            var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value);
            MoveUseCase.Move(ref transform.ValueRW, targetTransform.Position, speed.ValueRO.Value, deltaTime);
        }
    }
}