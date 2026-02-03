using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

partial struct AgentSetDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (target, body, entityTransform, entity) in SystemAPI
                     .Query<RefRO<Target>, RefRW<AgentBody>, RefRO<LocalTransform>>()
                     .WithEntityAccess())
        {
            Entity targetEntity = target.ValueRO.Value;

            // ОБЯЗАТЕЛЬНЫЕ ПРОВЕРКИ
            if (targetEntity == Entity.Null ||
                !SystemAPI.Exists(targetEntity) ||
                !SystemAPI.HasComponent<LocalTransform>(targetEntity))
                continue;

            var targetTransform = SystemAPI.GetComponentRO<LocalTransform>(targetEntity);

            float distance = math.distance(
                entityTransform.ValueRO.Position,
                targetTransform.ValueRO.Position
            );

            if (distance < 1.1f)
                ecb.AddComponent(entity, new AnimationRequest { AnimationName = "Attack" });
            else
                ecb.AddComponent(entity, new AnimationRequest { AnimationName = "Walk" });

            body.ValueRW.SetDestination(targetTransform.ValueRO.Position);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}