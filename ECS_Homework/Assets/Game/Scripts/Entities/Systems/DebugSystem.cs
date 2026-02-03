// using Game.Scripts.Entities.Systems;
// using Unity.Entities;
// using Unity.Mathematics;
// using Unity.Collections;
// using Unity.Transforms;
// using UnityEngine;
// using ProjectDawn.Navigation;
//
// public partial struct DebugSystem : ISystem
// {
//     public void OnUpdate(ref SystemState state)
//     {
//         var ecb = new EntityCommandBuffer(Allocator.Temp);
//
//         foreach (var (agent, target, entity) in SystemAPI.Query<RefRO<AgentBody>, RefRO<Target>>().WithEntityAccess())
//         {
//             // скорость агента
//             float3 speed = agent.ValueRO.Velocity;
//
//             // позиции
//             var targetTransform = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value);
//             var entityTransform = state.EntityManager.GetComponentData<LocalTransform>(entity);
//
//             // расстояние до цели
//             float distance = math.distance(entityTransform.Position, targetTransform.Position);
//
//             // порог для остановки: скорость почти ноль и близко к цели
//             // if (math.length(speed) < 0.01f && distance < 2)
//             // {
//             //     Debug.Log("Stop — reached target");
//             //
//             //     ecb.AddComponent(entity, new AttackRequest());
//             // }
//         }
//
//         ecb.Playback(state.EntityManager);
//         ecb.Dispose();
//     }
// }