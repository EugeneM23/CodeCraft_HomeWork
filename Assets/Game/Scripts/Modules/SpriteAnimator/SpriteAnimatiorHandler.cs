using UnityEngine;

namespace Gameplay
{
    public class SpriteAnimatiorHandler : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private SpriteAnimation[] _animations;

        private SpriteAnimator _animator;
        private bool _enable;

        private void OnEnable()
        {
            _animator = new SpriteAnimator(_animations, _spriteRenderer);
            _animator.Initialize();
            _enable = true;
        }

        private void OnDisable() => _enable = false;

        private void Update()
        {
            if (_enable)
                _animator.Tick();
        }
    }
}