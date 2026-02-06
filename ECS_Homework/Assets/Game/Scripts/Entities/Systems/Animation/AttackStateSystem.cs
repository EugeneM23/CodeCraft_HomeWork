// using Game.Animation;
// using Rukhanka;
// using Unity.Entities;
// using UnityEngine;
//
// public partial struct AttackStateSystem : ISystem
// {
//     public void OnUpdate(ref SystemState state)
//     {
//         var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
//
//         foreach (var (animParams, indexes, entity) in SystemAPI
//                      .Query<DynamicBuffer<AnimatorControllerParameterComponent>,
//                          AnimatorControllerParameterIndexTableComponent>()
//                      .WithEntityAccess()
//                      .WithAll<AttackStateRequset>()
//                      .WithNone<AttackState>())
//         {
//             var animator = new AnimatorParametersAspect(animParams, indexes);
//
//             Debug.Log("Attacking");
//             animator.SetParameterValue("Attack", true);
//
//             ecb.RemoveComponent<AttackStateRequset>(entity);
//
//             ecb.AddComponent<AttackState>(entity);
//         }
//
//         ecb.Playback(state.EntityManager);
//         ecb.Dispose();
//     }
// }