using System;
using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

partial struct AgentSetDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        foreach (var (target, body, newUnit, entity) in SystemAPI
                     .Query<RefRO<Target>, RefRW<AgentBody>, RefRW<NewUnitTag>>().WithEntityAccess())
        {
            if (target.ValueRO.Value == Entity.Null) continue;

            var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value);
            body.ValueRW.SetDestination(targetTransform.Position);

            ecb.RemoveComponent<NewUnitTag>(entity);
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}