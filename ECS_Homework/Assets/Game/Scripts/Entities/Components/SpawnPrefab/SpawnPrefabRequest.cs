using Unity.Entities;
using Unity.Mathematics;

public struct SpawnPrefabRequest : IComponentData
{
    public float3 Position;
    public Entity Prefab;
}