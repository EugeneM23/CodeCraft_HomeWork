using Unity.Entities;
using Unity.Mathematics;

public struct SpawnPrefabRequest : IComponentData
{
    public float3 Position;
    public quaternion Rotation;
    public Entity Prefab;
}