using Unity.Entities;

internal struct LifeTime : IComponentData
{
    public float Value;
    public float TimeLeft;
}