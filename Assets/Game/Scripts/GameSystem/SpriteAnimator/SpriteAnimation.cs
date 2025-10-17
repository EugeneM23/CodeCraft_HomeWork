using UnityEngine;

namespace Game.Scripts.Modules.SpriteAnimator
{
    [CreateAssetMenu(fileName = "Animation", menuName = "Game/Sprite Animation")]
    public class SpriteAnimation : ScriptableObject
    {
        [field: SerializeField] public Sprite[] Sprites { get; private set; }
        [field: SerializeField] public AnimationID ID { get; private set; }
        [field: SerializeField] public float FPS { get; private set; }
        public bool CanInterrupt = true;

        public SpriteAnimationEvent Event = new();

        public void PlayEvents(int currentFrame)
        {
            if (Event.Event == null) return;

            if (currentFrame == Event.Frame)
                Event.Event.Invoke();
        }
    }
}