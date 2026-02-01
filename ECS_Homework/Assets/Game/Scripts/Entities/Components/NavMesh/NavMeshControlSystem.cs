using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

public partial struct NavMeshControlSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (navAgent, moveCommand) in
                 SystemAPI.Query<NavMeshAgentComponent, RefRO<MoveToCommand>>())
        {
            Debug.Log(
                $"enabled={navAgent.Agent.enabled}, " +
                $"isOnNavMesh={navAgent.Agent.isOnNavMesh}, " +
                $"pos={navAgent.Agent.transform.position}"
            );
            Debug.Log(NavMesh.SamplePosition(
                navAgent.Agent.transform.position,
                out var hit,
                10f,
                NavMesh.AllAreas
            ));
            if (!navAgent.Agent.enabled || !navAgent.Agent.isOnNavMesh)
                continue;

            if (moveCommand.ValueRO.HasDestination)
            {
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

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct NavMeshInitSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var navAgent in
                 SystemAPI.Query<NavMeshAgentComponent>())
        {
            var agent = navAgent.Agent;

            if (agent == null)
                continue;

            if (agent.isOnNavMesh)
                continue;

            if (!agent.enabled)
                continue;

            NavMeshHit hit;
            if (NavMesh.SamplePosition(
                    agent.transform.position,
                    out hit,
                    10f,
                    NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log("WARP OK");
            }
        }
    }
}