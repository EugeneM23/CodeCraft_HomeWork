using Unity.Collections;
using Unity.Entities;

public struct AnimationRequest : IComponentData
{
    public FixedString64Bytes AnimationName;
}