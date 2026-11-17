using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A blinded enemy wandering: every second or so it turns to a random direction on the ground
    /// and shuffles that way at a fraction of its speed. The move direction is not normalized, so
    /// its length scales the speed (MoveUseCase multiplies it by MoveSpeed).
    /// </summary>
    public sealed class BlindStumbleBehaviour : IEntityInit, IEntityTick
    {
        private const float MIN_INTERVAL = 0.6f;
        private const float MAX_INTERVAL = 1.2f;

        private readonly float _speed;
        private IReactiveVariable<Vector3> _move;
        private IReactiveVariable<Vector3> _rotate;
        private float _untilTurn;

        public BlindStumbleBehaviour(float speed) => _speed = speed;

        public void Init(IEntity entity)
        {
            _move = entity.GetMoveDirection();
            _rotate = entity.GetRotateDirection();
            _untilTurn = 0f;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            _untilTurn -= deltaTime;

            if (_untilTurn > 0f)
                return;

            _untilTurn = Random.Range(MIN_INTERVAL, MAX_INTERVAL);

            Vector2 flat = Random.insideUnitCircle.normalized;
            Vector3 direction = new Vector3(flat.x, 0f, flat.y);

            _move.Value = direction * _speed;
            _rotate.Value = direction;
        }
    }
}
