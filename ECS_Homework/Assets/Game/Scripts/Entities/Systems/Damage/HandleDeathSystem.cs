using Game.Animation;
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
            ecb.AddComponent(entity, new AnimationEventRequest { Parameter = ACBase.Death.ToString() });
            ecb.RemoveComponent<DeathEvent>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}