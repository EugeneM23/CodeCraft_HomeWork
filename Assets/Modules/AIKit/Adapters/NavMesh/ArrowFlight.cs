using UnityEngine;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// A projectile in a straight flight (ArrowLauncher): goes straight, hits the target if it passes near it, ends at a
    /// wall (the shooter's sight probe says where a line is blocked) or at the end of its range. Where it ends is told to
    /// the launch's landing (an ability's effects, the end of its ground mark) — once.
    /// </summary>
    public sealed class ArrowFlight : MonoBehaviour
    {
        private Transform _attacker;
        private Transform _target;
        private ISightProbe _walls;
        private float _damage;
        private float _speed;
        private float _rangeLeft;
        private float _hitRadius;
        private float _aimHeight;
        private ProjectileLaunch _launch;
        private bool _told;

        /// <summary>The arrow hit its target (tests, effects).</summary>
        public bool Hit { get; private set; }

        /// <summary>The flight is over: hit, stopped by a wall or out of range.</summary>
        public bool Ended { get; private set; }

        /// <summary>A wall stopped it before the target (tests, effects: a spark, a thud).</summary>
        public bool HitWall { get; private set; }

        /// <summary>
        /// The wall stopped it short of the target (on the way there — the shot was taken into a wall), not after it flew
        /// past a target that had stepped aside.
        /// </summary>
        public bool HitWallShort { get; private set; }

        private float _toTarget;
        private float _flown;

        /// <summary>An arrow's flight is over (hit, stopped by a wall, out of range) — what a test or an effect listens to.</summary>
        public static event System.Action<ArrowFlight> Finished;

        /// <summary>The flight of a launch: its numbers, and its landing told where it ends.</summary>
        public void Launch(in ProjectileLaunch launch, float range)
        {
            WeaponShot shot = launch.Shot;
            Launch(launch.Shooter, launch.Target, launch.Damage, shot.Speed, range, shot.HitRadius, shot.AimHeight,
                launch.Sight);
            _launch = launch;
            _told = false;
        }

        public void Launch(Transform attacker, Transform target, float damage, float speed, float range, float hitRadius,
            float aimHeight, ISightProbe walls)
        {
            _attacker = attacker;
            _target = target;
            _damage = damage;
            _speed = speed;
            _rangeLeft = range;
            _flown = 0f;
            _toTarget = target != null ? Vector3.Distance(transform.position, target.position + Vector3.up * aimHeight) : range;
            _hitRadius = hitRadius;
            _aimHeight = aimHeight;
            _walls = walls;
            _launch = default;
            _told = true;                                   // no launch to tell (a bare flight)
        }

        private void Update() => Fly(Time.deltaTime);

        /// <summary>One step of the flight (Update calls it; a test can too).</summary>
        public void Fly(float dt)
        {
            if (Ended)
                return;

            float step = _speed * dt;
            Vector3 next = transform.position + transform.forward * step;

            // The target passed near: hit (before the wall check — a target standing at a wall is still hit)
            if (_target != null && Passes(transform.position, next))
            {
                Hit = true;
                if (_damage > 0f)
                    DirectStriker.Deliver(_attacker, _target, _damage);
                End();
                return;
            }

            if (_walls != null && !_walls.HasLineOfSight(transform.position, next))
            {
                HitWall = true;
                HitWallShort = _flown < _toTarget - _hitRadius;
                End();
                return;
            }

            transform.position = next;
            _rangeLeft -= step;
            _flown += step;
            if (_rangeLeft <= 0f)
                End();
        }

        // The segment of this step comes within the hit radius of the target's body (its pivot raised to the aim height)
        private bool Passes(Vector3 from, Vector3 to)
        {
            Vector3 body = _target.position + Vector3.up * _aimHeight;
            Vector3 along = to - from;
            float length = along.sqrMagnitude;
            float t = length > 1e-6f ? Mathf.Clamp01(Vector3.Dot(body - from, along) / length) : 0f;
            Vector3 offset = body - (from + along * t);
            offset.y *= 0.5f;                               // a body is taller than it is wide
            return offset.magnitude <= _hitRadius;
        }

        private void End()
        {
            Ended = true;
            if (!_told)
            {
                _told = true;
                _launch.Landing?.Landed(_launch, transform.position, Hit ? _target : null);
            }

            Finished?.Invoke(this);
            if (Application.isPlaying)
                Destroy(gameObject);
        }
    }
}
