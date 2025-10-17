using System;
using System.Linq;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.Modules.SpriteAnimator
{
    public class SpriteAnimator : ITickable, IInitializeble
    {
        private SpriteAnimation[] _animation;
        private SpriteRenderer _spriteRenderer;

        public SpriteAnimation CurrentAnimation => _currentAnimation;
        private SpriteAnimation _currentAnimation;

        private float _frameTime; // накопленное время
        private int _currentFrame;

        private float FrameDuration => 1f / _currentAnimation.Speed;

        public SpriteAnimator(SpriteAnimation[] animation, SpriteRenderer spriteRenderer)
        {
            _animation = animation;
            _spriteRenderer = spriteRenderer;
        }

        public void Initialize() => _currentAnimation = _animation[0];

        public void Tick()
        {
            _frameTime += Time.deltaTime;

            if (_frameTime < FrameDuration)
                return;

            _frameTime -= FrameDuration; 

            _currentAnimation.PlayEvents(_currentFrame);

            _spriteRenderer.sprite = _currentAnimation.Sprites[_currentFrame++];
            if (_currentFrame >= _currentAnimation.Sprites.Length)
                _currentFrame = 0;
        }

        public SpriteAnimator Play(AnimationName name)
        {
            if (_currentAnimation.Name != name)
            {
                _currentAnimation = _animation.FirstOrDefault(x => x.Name == name);
                _currentFrame = 0;
                _frameTime = 0;
            }

            return this;
        }

        public SpriteAnimator AddEvent(Action action, int frame)
        {
            _currentAnimation.Event.Event = action;
            _currentAnimation.Event.Frame = frame;
            return this;
        }
    }
}