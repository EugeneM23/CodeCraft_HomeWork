using UnityEngine;

namespace Gameplay
{
    public class AttackComponent : CompositCondition, IInitializeble
    {
        private InputReader _inputReader;
        private Rigidbody2D _rigidbody;

        [Inject]
        private void Construct(InputReader inputReader, Rigidbody2D rigidbody, CollisionComponent collision)
        {
            _rigidbody = rigidbody;
            _inputReader = inputReader;
        }

        public void Initialize()
        {
            _inputReader.OnFire += Attack;
        }

        private void Attack()
        {
            if (!AndCondition())
                _rigidbody.linearVelocity = Vector2.up * 5;
        }
    }
}