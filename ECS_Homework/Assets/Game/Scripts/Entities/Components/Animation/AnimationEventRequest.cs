using Unity.Collections;
using Unity.Entities;

public struct AnimationEventRequest : IComponentData
{
    public FixedString64Bytes Parameter;
}