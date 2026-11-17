using UnityEngine;

namespace AIKit.NavMeshNavigation
{
    /// <summary>
    /// A projectile in a lob (ArrowLauncher, step 90 — a thrown rock, a grenade): an arc from where it left to where the
    /// target stood when it was let go (or to the point), as fast as the shot or in the launch's own flight time. Where it
    /// comes down it hits the target if the target is still within the shot's hit radius — step aside and it misses —
    /// tells the launch's landing where it came down (an ability's area damage, the end of its ground mark), and is gone:
    /// a thrown rock does not lie about to be picked up again (C14).
    /// </summary>
    public sealed class ArcFlight : MonoBehaviour
    {
        private ProjectileLaunch _launch;
        private Vector3 _from;
        private Vector3 _to;
        private float _time;
        private float _duration;
        private float _height;

        /// <summary>It is in the air.</summary>
        public bool IsFlying { get; private set; }

        /// <summary>It came down (the flight is over).</summary>
        public bool Ended { get; private set; }

        /// <summary>It came down near the target and hit it (tests, effects).</summary>
        public bool Hit { get; private set; }

        /// <summary>Where it comes down.</summary>
        public Vector3 LandsAt => _to;

        /// <summary>Seconds the flight lasts.</summary>
        public float Duration => _duration;

        /// <summary>A lob is over (it came down) — what a test or an effect listens to.</summary>
        public static event System.Action<ArcFlight> Finished;

        public void Launch(in ProjectileLaunch launch, Vector3 from)
        {
            _launch = launch;
            _from = from;
            _to = launch.AimPoint;
            float distance = Vector3.Distance(_from, _to);
            float speed = launch.Shot != null ? Mathf.Max(1f, launch.Shot.Speed) : 10f;
            _duration = launch.Flight > 0f ? launch.Flight : Mathf.Max(0.1f, distance / speed);
            _height = distance * launch.Arc;
            _time = 0f;
            Hit = false;
            Ended = false;
            IsFlying = true;
            transform.position = _from;
        }

        private void Update() => Fly(Time.deltaTime);

        /// <summary>One step of the flight (Update calls it; a test can too).</summary>
        public void Fly(float dt)
        {
            if (!IsFlying)
                return;

            _time += dt;
            float t = Mathf.Clamp01(_time / _duration);
            Vector3 point = Vector3.Lerp(_from, _to, t);
            point.y += Mathf.Sin(t * Mathf.PI) * _height;
            transform.position = point;
            transform.Rotate(Vector3.right, 540f * dt, Space.Self);
            if (t < 1f)
                return;

            IsFlying = false;
            Ended = true;
            Transform struck = null;
            Transform target = _launch.Target;
            if (target != null)
            {
                Vector3 off = target.position - _to;
                off.y = 0f;
                if (off.magnitude <= (_launch.Shot != null ? _launch.Shot.HitRadius : 0f))
                    struck = target;
            }

            Hit = struck != null;
            if (Hit && _launch.Damage > 0f)
                DirectStriker.Deliver(_launch.Shooter, struck, _launch.Damage);

            transform.position = _to;
            _launch.Landing?.Landed(_launch, _to, struck);
            Finished?.Invoke(this);
            if (Application.isPlaying)
                Destroy(gameObject);                    // gone where it came down
        }
    }
}
