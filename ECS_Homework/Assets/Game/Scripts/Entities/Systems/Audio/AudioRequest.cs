using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public struct AudioRequest : IComponentData
{
    public FixedString32Bytes SoundName;
    public Entity Target;
    public float3 Position;
}