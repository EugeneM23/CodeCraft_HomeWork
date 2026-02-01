using Unity.Entities;
using Unity.Mathematics;

public struct SpawnUnitRequest : IComponentData
{
    public float3 Position;
    public Entity UnitPrefab;
}