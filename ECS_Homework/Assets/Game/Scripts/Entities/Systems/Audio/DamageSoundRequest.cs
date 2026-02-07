using Unity.Collections;
using Unity.Entities;

public struct DamageSoundRequest : IComponentData
{
    public FixedString32Bytes SoundName;
    public Entity Target;
}