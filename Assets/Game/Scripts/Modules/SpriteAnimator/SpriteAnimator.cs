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

        private SpriteAnimation _currenAnimation;
        private float _animationSpeed => _currenAnimation.Speed;
        private float _frametime;
        private int _currentFrame;

        public SpriteAnimator(SpriteAnimation[] animation, SpriteRenderer spriteRenderer)
        {
            _animation = animation;
            _spriteRenderer = spriteRenderer;
        }

        public void Initialize() => _currenAnimation = _animation[0];

        public void Tick()
        {
            _frametime += Time.deltaTime;
            if (_frametime < _animationSpeed) return;
            
            _currenAnimation.PlayEvents(_currentFrame);

            _frametime = 0;
            _spriteRenderer.sprite = _currenAnimation.Sprites[_currentFrame++];
            if (_currentFrame >= _currenAnimation.Sprites.Length)
                _currentFrame = 0;
        }

        public SpriteAnimator Play(AnimationName name)
        {
            if (_currenAnimation.Name != name)
            {
                _currenAnimation = _animation.FirstOrDefault(x => x.Name == name);
                _currentFrame = 0;
            }

            return this;
        }

        public SpriteAnimator AddEvent(Action action, int frame)
        {
            _currenAnimation.Event.Event = action;
            _currenAnimation.Event.Frame = frame;

            return this;
        }
    }
}