using AudioEngine;
using Game.Scripts.Entities.Systems;
using Game.Scripts.Entities.Systems.Request;
using Game.Scripts.UI.Entities.Components.Health;
using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateAfter(typeof(ApplyDamageSystem))]
public partial struct UnitDeathObserverSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (health, entity) in SystemAPI.Query<RefRO<Health>>().WithEntityAccess().WithNone<IsDead>())
        {
            if (health.ValueRO.Value <= 0)
            {
                ecb.AddComponent<IsDead>(entity);
                ecb.RemoveComponent<Target>(entity);
                ecb.RemoveComponent<AgentBody>(entity);
                ecb.RemoveComponent<TargetDistance>(entity);
                ecb.RemoveComponent<TeamMask>(entity);

                RequestUseCase.PlaySound(ecb, entity, MasterBankAPI.DeathEvent, new float3());
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}