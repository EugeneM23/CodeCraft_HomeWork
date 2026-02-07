using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Collections;
using Unity.Entities;

[UpdateAfter(typeof(CheckDeathSystem))]
public partial struct HandleDeathSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (_, entity) in SystemAPI.Query<DeathEvent>().WithEntityAccess())
        {
            ecb.RemoveComponent<Target>(entity);
            ecb.RemoveComponent<AgentBody>(entity);
            ecb.RemoveComponent<TeamMask>(entity);
            ecb.RemoveComponent<DeathEvent>(entity);
            
            //RequestUseCase.PlaySound(ecb, entity, MasterBankAPI.DeathEvent, new float3());q
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}