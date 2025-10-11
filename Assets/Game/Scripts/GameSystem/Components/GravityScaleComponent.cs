using UnityEngine;

namespace Gameplay
{
    public class GravityScaleComponent : IInitializeble
    {
        private float _gravityScale = 1f;
        private Rigidbody2D _rigidbody2D;

        public GravityScaleComponent(Rigidbody2D rigidbody2D, float gravityScale = 1f)
        {
            _gravityScale = gravityScale;
            _rigidbody2D = rigidbody2D;
        }

        public void Initialize() => _rigidbody2D.gravityScale = _gravityScale;
    }
}