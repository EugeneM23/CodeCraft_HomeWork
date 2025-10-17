using System;

namespace Game.Scripts.Modules.SpriteAnimator
{
    public struct SpriteAnimationEvent
    {
        public Action Event { get; set; }
        public int Frame { get; set; }
    }
}