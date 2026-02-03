using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct AgentSetDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (target, body, entity) in SystemAPI
                     .Query<RefRO<Target>, RefRW<AgentBody>>().WithEntityAccess())
        {
            if (target.ValueRO.Value == Entity.Null) continue;


            var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value);
            var entityTransform = state.EntityManager.GetComponentData<LocalTransform>(entity);

            var distance = math.distance(entityTransform.Position, targetTransform.Position);

            if (distance < 1.1f)
                ecb.AddComponent(entity, new AttackRequest());

            body.ValueRW.SetDestination(targetTransform.Position);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}