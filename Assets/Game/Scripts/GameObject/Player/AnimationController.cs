using Gameplay;
using Unity.VisualScripting;
using UnityEngine;

namespace Game.Scripts.Modules.SpriteAnimator
{
    public class AnimationController : ITickable, IInitializable, IDisposable
    {
        private CollisionComponent _collisionComponent;
        private Rigidbody2D _rigidbody2D;
        private InputReader _inputReader;
        private SpriteAnimator _animator;
        private float _attackTime;
        private bool _isAttacking;

        [Inject]
        public void Construct(CollisionComponent collisionComponent, Rigidbody2D rigidbody2D, InputReader inputReader,
            SpriteAnimator animator)
        {
            _animator = animator;
            _collisionComponent = collisionComponent;
            _rigidbody2D = rigidbody2D;
            _inputReader = inputReader;
            _collisionComponent.OnFlying += OnFall;
            _inputReader.OnFire += Attack;
        }

        public void Initialize()
        {
            _collisionComponent.OnFlying += OnFall;
            _inputReader.OnFire += Attack;
        }

        public void Dispose()
        {
            _collisionComponent.OnFlying -= OnFall;
            _inputReader.OnFire -= Attack;
        }

        private void Attack()
        {
            if (_isAttacking) return;
            
            _animator
                .Play(AnimationName.Attack)
                .AddEvent((() => "Debug/log".Log(Color.cyan)), 3);
            
            _isAttacking = true;
        }

        private void OnFall() => _animator.Play(AnimationName.Fall);

        public void Tick()
        {
            if (_isAttacking)
            {
                _attackTime += Time.deltaTime;
                if (_attackTime >= 0.5f)
                {
                    _isAttacking = false;
                    _attackTime = 0;
                }

                return;
            }


            if (!_collisionComponent.IsGrounded)
            {
                _animator.Play(AnimationName.Fall);
                return;
            }

            float speed = Mathf.Abs(_rigidbody2D.linearVelocity.x);

            if (speed > 1f)
                _animator.Play(AnimationName.Run);
            else
                _animator.Play(AnimationName.Idle);
        }
    }
}