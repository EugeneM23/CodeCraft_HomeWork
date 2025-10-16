using UnityEngine;

namespace Gameplay
{
    public class AttackComponent : CompositCondition, IInitializeble
    {
        [Inject] private readonly InputReader _inputReader;
        private readonly Rigidbody2D _rigidbody;

        public AttackComponent(Rigidbody2D rigidbody)
        {
            _rigidbody = rigidbody;
        }

        public void Initialize()
        {
            _inputReader.OnFire += Attack;
        }

        private void Attack()
        {
            if (!IsTrue())
                _rigidbody.linearVelocity = Vector2.up * 5;
        }
    }
}