using System;
using Sirenix.OdinInspector;

namespace Gameplay
{
    [Serializable]
    public struct SpriteAnimationEvent
    {
        [HorizontalGroup("Row"), LabelWidth(40)]
        [ShowInInspector]
        public EventID ID { get; set; }
        [HorizontalGroup("Row"), LabelWidth(40)]
        [ShowInInspector] public int Frame { get; set; }
    }
}