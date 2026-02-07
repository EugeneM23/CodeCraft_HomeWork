using Unity.Entities;

public struct TargetDistance : IComponentData
{
    public float Value;

    public TargetDistance(float value)
    {
        Value = value;
    }
}