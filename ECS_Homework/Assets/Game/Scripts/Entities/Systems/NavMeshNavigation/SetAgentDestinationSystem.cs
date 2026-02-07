using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Entities;
using Unity.Transforms;

public partial struct SetAgentDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);

        foreach (var (target, body) in SystemAPI.Query<RefRO<Target>, RefRW<AgentBody>>())
        {
            if (target.ValueRO.Value == Entity.Null) continue;
            if (!transformLookup.HasComponent(target.ValueRO.Value)) continue;

            body.ValueRW.SetDestination(transformLookup[target.ValueRO.Value].Position);
        }
    }
}