using Unity.Entities;

public struct DamageRequest : IComponentData
{
    public Entity Target;
    public int DamageAmount;
}