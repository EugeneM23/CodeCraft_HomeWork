using System;
using System.Linq;
using UnityEngine;

namespace Gameplay
{
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private AnimationEventReceiver _animationEventReceiver;
        [SerializeField] private SpriteAnimation[] _animation;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        public SpriteAnimation CurrentAnimation => _currentAnimation;
        private SpriteAnimation _currentAnimation;

        private float _frameTime;
        private int _currentFrame;

        private float FrameDuration => 1f / _currentAnimation.FPS;

        public void Start() => _currentAnimation = _animation[0];

        public void Update()
        {
            _frameTime += Time.deltaTime;

            if (_frameTime < FrameDuration)
                return;

            UpdateSprite();
            SendEvents();
        }

        private void UpdateSprite()
        {
            _frameTime -= FrameDuration;
            _spriteRenderer.sprite = _currentAnimation.Sprites[_currentFrame++];
            if (_currentFrame >= _currentAnimation.Sprites.Length)
            {
                _currentFrame = 0;
                _currentAnimation.CanInterrupt = true;
            }
        }

        private void SendEvents()
        {
            var eventIds = _currentAnimation.GetEvents(_currentFrame);

            foreach (EventID item in eventIds)
                if (item != EventID.None)
                    _animationEventReceiver.SendEvent(item);
        }

        public SpriteAnimator Play(AnimationID id)
        {
            if (_currentAnimation.ID != id && _currentAnimation.CanInterrupt)
            {
                _currentAnimation = _animation.FirstOrDefault(x => x.ID == id);
                _currentFrame = 0;
                _frameTime = 0;
            }

            return this;
        }

        public SpriteAnimator Interrupt(bool interrupt)
        {
            _currentAnimation.CanInterrupt = interrupt;
            return this;
        }

        public bool Lock()
        {
            return !_currentAnimation.CanInterrupt;
        }
    }
}

