using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class MoveComponent : IMoveComponent
    {
        private readonly CollisionComponent _collision;
        private readonly InertiaComponent _inertia;

        public MoveComponent(CollisionComponent collision, InertiaComponent inertia)
        {
            _collision = collision;
            _inertia = inertia;
        }

        public Vector2 Move(Vector2 direction)
        {
            if (_collision.IsGrounded)
                return MoveOnGround(direction);
            
            return MoveInAir(direction);
        }

        private Vector2 MoveInAir(Vector2 direction)
        {
            float speed = _inertia.CalculateSpeed(direction.x, false);
            return new Vector2(speed, 0);
        }

        private Vector2 MoveOnGround(Vector2 direction)
        {
            Vector2 normal = _collision.SurfaceNormal == Vector2.zero ? Vector2.up : _collision.SurfaceNormal;
            Vector2 tangent = new Vector2(normal.y, -normal.x).normalized;

            float speed = _inertia.CalculateSpeed(direction.x, true);
            return tangent * speed;
        }
    }
}

