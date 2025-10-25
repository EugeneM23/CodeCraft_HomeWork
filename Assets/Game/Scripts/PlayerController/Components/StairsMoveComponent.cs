using UnityEngine;

namespace Game.Scripts.PlayerController
{
    internal class StairsMoveComponent : IMoveComponent
    {
        private readonly CollisionComponent _collision;
        private readonly ScriptableStats _stats;
        private readonly PlayerController _player;

        private float _currentHorizontalSpeed;
        private float _moveY;

        public StairsMoveComponent(ScriptableStats stats, CollisionComponent collision, PlayerController player)
        {
            _stats = stats;
            _collision = collision;
            _player = player;
        }

        public Vector2 Move(Vector2 direction)
        {
            if (!_player.IsOnStairs) return Vector2.zero;

            if (direction.y != 0)
                _moveY = direction.y * 5;
            else
                return Vector2.zero;

            return new Vector2(0, _moveY);
        }
    }
}