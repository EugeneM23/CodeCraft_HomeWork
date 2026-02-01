using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshAgentAuthoring : MonoBehaviour
{
    public NavMeshAgent Agent;
    public class NavMeshAgentBake : Baker<NavMeshAgentAuthoring>
    {
        public override void Bake(NavMeshAgentAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponentObject(entity, new NavMeshAgentComponent { Agent = authoring.Agent });
            AddComponent(entity, new MoveToCommand { HasDestination = true, Destination = new float3(11f, 0, 1f) });
        }
    }
}

public class NavMeshAgentComponent : IComponentData
{
    public NavMeshAgent Agent;
}

public struct MoveToCommand : IComponentData
{
    public float3 Destination;
    public bool HasDestination;
}