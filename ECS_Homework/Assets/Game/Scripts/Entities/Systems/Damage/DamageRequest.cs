using Unity.Entities;

public struct DamageRequest : IComponentData
{
    public Entity Target;
    public Entity Attacker;
    public int DamageAmount;
}