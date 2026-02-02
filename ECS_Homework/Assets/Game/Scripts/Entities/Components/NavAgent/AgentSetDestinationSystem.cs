using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
partial struct AgentSetDestinationSystem : ISystem
{
    [BurstCompile]

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (target, body) in SystemAPI.Query<RefRO<Target>, RefRW<AgentBody>>())
        {
            if (target.ValueRO.Value == Entity.Null) continue;

            var localTransform = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value);
            body.ValueRW.SetDestination(localTransform.Position);
            //
            // if (body.ValueRW.RemainingDistance is > 0 and <= 3f)
            //     body.ValueRW.Stop();
        }
    }
}