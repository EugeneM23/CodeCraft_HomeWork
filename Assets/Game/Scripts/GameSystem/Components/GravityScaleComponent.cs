using UnityEngine;

namespace Gameplay
{
    public class GravityScaleComponent : IInitializeble
    {
        private readonly float _gravityScale;
        private readonly Rigidbody2D _rigidbody2D;

        public GravityScaleComponent(Rigidbody2D rigidbody2D, float gravityScale)
        {
            _gravityScale = gravityScale;
            _rigidbody2D = rigidbody2D;
        }
       

        public void Initialize() => _rigidbody2D.gravityScale = _gravityScale;
    }
}