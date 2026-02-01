using Unity.Entities;
using Unity.Transforms;

public partial struct NavMeshControlSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (navAgent, moveCommand) in
                 SystemAPI.Query<NavMeshAgentComponent, RefRO<MoveToCommand>>())
        {
            if (navAgent.Agent != null && moveCommand.ValueRO.HasDestination)
            {
                if (navAgent.Agent.isOnNavMesh) 
                    navAgent.Agent.SetDestination(moveCommand.ValueRO.Destination);
            }
        }
    }
}

public partial struct NavMeshSyncSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (navAgent, transform) in
                 SystemAPI.Query<NavMeshAgentComponent, RefRW<LocalTransform>>())
        {
            if (navAgent.Agent != null && navAgent.Agent.isOnNavMesh)
            {
                transform.ValueRW.Position = navAgent.Agent.transform.position;
                transform.ValueRW.Rotation = navAgent.Agent.transform.rotation;
            }
        }
    }
}