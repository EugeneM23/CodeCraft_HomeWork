using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using Unity.Profiling;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Flies a bullet along its MoveDirection at its MoveSpeed every physics step, swept: at
    /// 20 m/s a step is 0.4 m, longer than a brick wall is thick (MoveUseCase.MoveSwept).
    /// The step's segment is also checked against the enemies without colliders (the horde,
    /// <see cref="EnemyTargets.RaycastNonPhysical"/>): a monster on it is hit
    /// (<see cref="BulletEnemyHitUseCase"/>). Entity enemies are met through physics, as before.
    /// </summary>
    /// <remarks>
    /// Replaces a WhenFixedTick lambda that called MoveSelfSwept with the entity: the condition, the
    /// direction, the speed and the transform looked up on every step of every bullet in flight, and
    /// the body's collider fetched with GetComponent on every hit. All of them are looked up once
    /// here, when the pooled bullet is first rented. The move itself is the same.
    /// </remarks>
    public sealed class BulletMoveBehaviour : IEntityInit, IEntityEnable, IEntityFixedTick
    {
        /// <summary>Radius (m) of the tunnel a bullet clears through voxel smoke, as in CS2.</summary>
        private const float BULLET_SMOKE_TUNNEL = 0.5f;

        /// <summary>Radius (m) of the tunnel an explosive shell clears through voxel smoke.</summary>
        private const float SHELL_SMOKE_TUNNEL = 1f;

        /// <summary>Radius (m) and push (m/s) of a bullet's tunnel through the small voxel clouds (muzzle smoke).</summary>
        private const float BULLET_PUFF_TUNNEL = 1.2f;
        private const float SHELL_PUFF_TUNNEL = 2f;
        private const float BULLET_PUFF_PUSH = 0.8f;

        /// <summary>Radius (m) of the ball a bullet is taken for against horde monsters.</summary>
        private const float ENEMY_HIT_RADIUS = 0.3f;

        /// <summary>A piercing shell looks for the next monster this far (m) past the one it went through.</summary>
        private const float PIERCE_SKIP = 1.3f;

        /// <summary>Most monsters one step of a piercing shell goes through.</summary>
        private const int MAX_HITS_PER_STEP = 4;

        private readonly Rigidbody _body;
        private float _smokeTunnel;

        private IEntity _entity;
        private Transform _transform;
        private IValue<Vector3> _direction;
        private IValue<float> _speed;
        private IExpression<bool> _condition;
        private Collider _own;

        private GameFactory _factory;
        private GameContext _context;
        private IEnemyTargets _targetsSeen;
        private EnemyTargets _targets;
        private bool _noContextThisFlight;
        private EnemyRef _pierced = EnemyRef.None;

        /// <param name="body">The bullet's body, swept along the step; null moves it without a sweep.</param>
        public BulletMoveBehaviour(Rigidbody body) => _body = body;

        public void Init(IEntity entity)
        {
            _entity = entity;
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

        /// <summary>Rented from the pool: a new shot has pierced nothing yet.</summary>
        public void Enable(IEntity entity)
        {
            _pierced = EnemyRef.None;
            _noContextThisFlight = false;
        }

        private static readonly ProfilerMarker MoveMarker = new("Bullet.Move");

        public void FixedTick(IEntity entity, float deltaTime)
        {
            if (_condition != null && !_condition.Value)
                return;

            using (MoveMarker.Auto())
                Move(entity, deltaTime);
        }

        private void Move(IEntity entity, float deltaTime)
        {
            Vector3 from = _transform.position;
            MoveUseCase.MoveSwept(_transform, _body, _own, _direction.Value * _speed.Value * deltaTime);
            Vector3 to = _transform.position;

            // The sweep stopped the step at a wall: monsters behind it are not hit.
            if (HitEnemies(from, to))
                return;

            // A bullet through voxel smoke leaves a tunnel behind it that closes in about a second.
            if (VoxelSmoke.Active.Count > 0)
                VoxelSmoke.Cut(from, to, _smokeTunnel);
            // Experiment: the small voxel clouds (muzzle smoke, exhaust) part round it too, except the smoke of
            // its own shot (the weapon that fired it marks it).
            // A plain bullet (the machine gun) shoves the smoke harder and wider than its tunnel through a smoke screen.
            object weapon = entity.TryGetBulletWeapon(out IVariable<IEntity> holder) ? holder.Value : null;
            bool plain = _smokeTunnel < SHELL_SMOKE_TUNNEL;
            VoxelPuffField.Cut(from, to, plain ? BULLET_PUFF_TUNNEL : SHELL_PUFF_TUNNEL, weapon, plain ? BULLET_PUFF_PUSH : 0f);
        }

        /// <summary>True when the bullet was spent on a monster (already back in the pool).</summary>
        private bool HitEnemies(Vector3 from, Vector3 to)
        {
            LookUpTargets();
            if (_targets == null)
                return false;

            Vector3 path = to - from;
            float length = path.magnitude;
            if (length < 0.0001f)
                return false;
            Vector3 direction = path / length;

            Vector3 start = from;
            for (int i = 0; i < MAX_HITS_PER_STEP; i++)
            {
                if (!_targets.RaycastNonPhysical(start, to, ENEMY_HIT_RADIUS, out EnemyRef target, out Vector3 point))
                    return false;

                if (!SameEnemy(target, _pierced))
                {
                    if (BulletEnemyHitUseCase.Hit(_entity, _targets, target, point, _factory))
                        return true;
                    _pierced = target;
                }

                // Through it: look on past the monster for the rest of the step.
                start = point + direction * PIERCE_SKIP;
                if (Vector3.Dot(to - start, direction) <= 0f)
                    return false;
            }

            return false;
        }

        // Every step, cheaply: a pooled bullet outlives a GameContext restarted in the same scene, and the level's
        // combat may be reinstalled with new EnemyTargets. A live cached context costs one native null check and one
        // key lookup; a dead or missing one is looked up again, at most once per flight while there is none
        // (TryGetInstance searches the scene when it has no instance).
        private void LookUpTargets()
        {
            if (_context == null)
            {
                _targetsSeen = null;
                _targets = null;
                _factory = null;
                if (_noContextThisFlight)
                    return;
                if (!GameContext.TryGetInstance(out _context) || _context == null)
                {
                    _context = null;
                    _noContextThisFlight = true;
                    return;
                }

                _context.TryGetGameFactory(out _factory);
            }

            _context.TryGetEnemyTargets(out IEnemyTargets targets);
            if (ReferenceEquals(targets, _targetsSeen))
                return;

            _targetsSeen = targets;
            _targets = _factory != null ? targets as EnemyTargets : null;
        }

        private static bool SameEnemy(in EnemyRef a, in EnemyRef b) =>
            a.Source == b.Source && a.Index == b.Index && a.Version == b.Version;
    }
}
