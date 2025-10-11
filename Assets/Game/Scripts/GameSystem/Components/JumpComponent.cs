using UnityEngine;

namespace Gameplay
{
    public class JumpComponent : CompositCondition
    {
        private const int JUMP_COUNT = 2;

        private Rigidbody2D _rigidbody;
        private float _jumpForce = 20f;
        private int _jumpCount;

        public JumpComponent(float jumpForce, Rigidbody2D rigidbody)
        {
            _jumpForce = jumpForce;
            _rigidbody = rigidbody;
        }

        public void Jump()
        {
            if (AndCondition() || _jumpCount >= JUMP_COUNT)
                return;

            _rigidbody.ResetAndAddForce(Vector3.up * _jumpForce);

            _jumpCount++;
        }

        public void ResetJump() => _jumpCount = 0;
    }
}