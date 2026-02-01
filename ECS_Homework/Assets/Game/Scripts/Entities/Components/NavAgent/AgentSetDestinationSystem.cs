using Game.Scripts.Entities.Systems;
using ProjectDawn.Navigation;
using Unity.Entities;
using Unity.Transforms;

partial struct AgentSetDestinationSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (target, body) in SystemAPI.Query<RefRO<Target>, RefRW<AgentBody>>())
        {
            if (target.ValueRO.Value == Entity.Null) continue;

            var localTransform = state.EntityManager.GetComponentData<LocalTransform>(target.ValueRO.Value);
            body.ValueRW.SetDestination(localTransform.Position);
        }
    }
}