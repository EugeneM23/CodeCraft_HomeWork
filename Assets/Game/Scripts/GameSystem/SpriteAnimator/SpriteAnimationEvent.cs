using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace Gameplay
{
    public struct SpriteAnimationEvent
    {
        [OdinSerialize] public EventID ID { get; set; }

        [OdinSerialize] public int Frame { get; set; }
    }
}