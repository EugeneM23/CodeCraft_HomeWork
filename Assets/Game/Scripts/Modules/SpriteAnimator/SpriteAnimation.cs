using System;
using UnityEngine;

namespace Game.Scripts.Modules.SpriteAnimator
{
    [CreateAssetMenu(fileName = "Animation", menuName = "Game/Sprite Animation")]
    public class SpriteAnimation : ScriptableObject
    {
        [field: SerializeField] public Sprite[] Sprites { get; private set; }
        [field: SerializeField] public AnimationName Name { get; private set; }
        [field: SerializeField] public float Speed { get; private set; }

        public SpriteAnimationEvent Event = new();

        public void PlayEvents(int currentFrame)
        {
            if (Event.Event == null) return;

            if (currentFrame == Event.Frame) 
                Event.Event.Invoke();
        }
    }

    public struct SpriteAnimationEvent
    {
        public Action Event { get; set; }
        public int Frame { get; set; }
    }
}