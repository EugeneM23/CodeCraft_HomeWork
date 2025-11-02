using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Gameplay
{
    public class SpriteAnimator : IInitializeble, ITickable
    {
        private readonly AnimationEventReceiver _animationEventReceiver;
        private readonly SpriteAnimation[] _animation;
        private readonly SpriteRenderer _spriteRenderer;

        public SpriteAnimation CurrentAnimation => _currentAnimation;
        private SpriteAnimation _currentAnimation;

        private float _frameTime;
        private int _currentFrame;

        private float FrameDuration => 1f / _fps;
        private float _fps;

        public SpriteAnimator(SpriteAnimation[] animation, SpriteRenderer spriteRenderer,
            AnimationEventReceiver animationEventReceiver)
        {
            _spriteRenderer = spriteRenderer;
            _animationEventReceiver = animationEventReceiver;
            _animation = animation;
        }

        public void Initialize()
        {
            _currentAnimation = _animation[0];
        }

        public void Tick()
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

                if (_currentAnimation == null)
                    Debug.LogError($"Animation {id} not found");

                _fps = _currentAnimation.FPS;
                _currentFrame = 0;
                _frameTime = 0;
            }

            return this;
        }

        public SpriteAnimator PlayForce(AnimationID id)
        {
            if (_currentAnimation.ID != id)
            {
                _currentAnimation = _animation.FirstOrDefault(x => x.ID == id);

                if (_currentAnimation == null)
                    Debug.LogError($"Animation {id} not found");

                _fps = _currentAnimation.FPS;
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

        public void SetSpeed(float speed)
        {
            _fps = speed;
        }
    }
}