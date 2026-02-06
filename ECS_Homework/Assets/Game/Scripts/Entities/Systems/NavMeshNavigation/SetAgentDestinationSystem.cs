using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public partial struct SetAgentDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (target, body) in SystemAPI.Query<RefRO<Target>, RefRW<AgentBody>>())
        {
            var targetEntity = target.ValueRO.Value;

            if (targetEntity == Entity.Null)
                continue;

            if (!state.EntityManager.Exists(targetEntity))
                continue;

            if (!SystemAPI.HasComponent<LocalTransform>(targetEntity))
                continue;

            var targetTransform = SystemAPI.GetComponentRO<LocalTransform>(targetEntity);

            body.ValueRW.SetDestination(targetTransform.ValueRO.Position);
        }
    }
}