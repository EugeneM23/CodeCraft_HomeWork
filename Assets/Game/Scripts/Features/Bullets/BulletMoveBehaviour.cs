using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Flies a bullet along its MoveDirection at its MoveSpeed every physics step, swept: at
    /// 20 m/s a step is 0.4 m, longer than a brick wall is thick (MoveUseCase.MoveSwept).
    /// </summary>
    /// <remarks>
    /// Replaces a WhenFixedTick lambda that called MoveSelfSwept with the entity: the condition, the
    /// direction, the speed and the transform looked up on every step of every bullet in flight, and
    /// the body's collider fetched with GetComponent on every hit. All of them are looked up once
    /// here, when the pooled bullet is first rented. The move itself is the same.
    /// </remarks>
    public sealed class BulletMoveBehaviour : IEntityInit, IEntityFixedTick
    {
        /// <summary>Radius (m) of the tunnel a bullet clears through voxel smoke, as in CS2.</summary>
        private const float BULLET_SMOKE_TUNNEL = 0.5f;

        /// <summary>Radius (m) of the tunnel an explosive shell clears through voxel smoke.</summary>
        private const float SHELL_SMOKE_TUNNEL = 1f;

        private readonly Rigidbody _body;
        private float _smokeTunnel;

        private Transform _transform;
        private IValue<Vector3> _direction;
        private IValue<float> _speed;
        private IExpression<bool> _condition;
        private Collider _own;

        /// <param name="body">The bullet's body, swept along the step; null moves it without a sweep.</param>
        public BulletMoveBehaviour(Rigidbody body) => _body = body;

        public void Init(IEntity entity)
        {
            _transform = entity.GetTransform();
            _direction = entity.GetMoveDirection();
            _speed = entity.GetMoveSpeed();

            // Bullets install none; kept so one that does is still stopped by it, as before.
            entity.TryGetMoveCondition(out _condition);

            if (_body != null)
                _own = _body.GetComponent<Collider>();

            // A shell tears a wider hole through voxel smoke than a bullet does.
            _smokeTunnel = entity.HasExplosionRadius() ? SHELL_SMOKE_TUNNEL : BULLET_SMOKE_TUNNEL;
        }

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (_condition != null && !_condition.Value)
                return;

            Vector3 from = _transform.position;
            MoveUseCase.MoveSwept(_transform, _body, _own, _direction.Value * _speed.Value * deltaTime);

            // A bullet through voxel smoke leaves a tunnel behind it that closes in about a second.
            if (VoxelSmoke.Active.Count > 0)
                VoxelSmoke.Cut(from, _transform.position, _smokeTunnel);
        }
    }
}
