using UnityEngine;

namespace Gameplay
{
    public class RigidbodyForceComponent : CompositCondition
    {
        private readonly Rigidbody2D _rigidbody;
        [Inject] private Transform _transform;

        public RigidbodyForceComponent(Rigidbody2D rigidbody)
        {
            _rigidbody = rigidbody;
        }

        public void AddForce(Vector2 direction, float force)
        {
            if (AndCondition())
                return;
            
            Debug.DrawRay(_transform.position, direction * force, Color.red, 2f);
            _rigidbody.AddForce(direction * force, ForceMode2D.Impulse);
        }
    }
}