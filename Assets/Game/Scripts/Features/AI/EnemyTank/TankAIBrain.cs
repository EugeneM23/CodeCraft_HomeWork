using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Prototype (the user): a copy of the player's tank driven by the AI. It writes the same keys the player's
    /// controllers write — Driver, DriveInput, DriveBoost, RotateDirection — and pulls FireAction, so the tank drives,
    /// aims and shoots with all its own physics, recoil and effects. The steering, the aim, the shot and the way out of
    /// being stuck live here; what to drive to and what to shoot at is the subclass's (<see cref="Think"/>).
    /// </summary>
    public abstract class TankAIBrain : MonoBehaviour
    {
        protected const float FIRE_ANGLE = 4f;    // the turret is this close to the aim, degrees
        private const float FULL_TURN = 35f;      // hull angle off that gives a full stick, degrees
        private const float STUCK_SPEED = 0.6f;   // under this while throttling it is stuck, m/s
        private const float STUCK_TIME = 1.5f;
        private const float BACKUP_TIME = 1.6f;
        private const float AIM_HEIGHT = 1.2f;
        private const float CRANK_TIME = 3f;      // the engine cranks two seconds after it gets its driver

        private static readonly RaycastHit[] s_hits = new RaycastHit[8];

        protected MonoEntity Entity { get; private set; }
        protected Rigidbody Body { get; private set; }
        protected Transform Pivot { get; private set; }
        protected readonly PlayerTank Player = new();

        private IReactiveVariable<Vector2> _drive;
        private IReactiveVariable<bool> _boost;
        private IReactiveVariable<Vector3> _aim;

        private float _stuck;
        private float _backup;
        private float _backupTurn;
        private float _nextShot;
        private float _startedAt;
        private bool _counted;

        protected enum LineOfFire { Clear, ThroughVoxels, Blocked }

        /// <summary>Its own pause between shots on top of the reload, s.</summary>
        protected virtual Vector2 FirePause => new(1.2f, 3f);

        protected virtual void Start()
        {
            Entity = GetComponent<MonoEntity>();
            Body = GetComponent<Rigidbody>();
            _drive = Entity.GetDriveInput();
            Entity.TryGetDriveBoost(out _boost);
            _aim = Entity.GetRotateDirection();
            Pivot = Entity.GetTurretPivot();

            // It is the AI's to drive; the player's controllers never see it (it is not the player's character).
            Entity.GetDriver().Value = Entity;

            // One ear in the scene: the player's tank. The driver behaviour made one here too.
            if (TryGetComponent(out AudioListener ear))
                Destroy(ear);

            Count(+1);
            _counted = true;
            _startedAt = Time.time;
            _nextShot = Time.time + CRANK_TIME;
        }

        /// <summary>The sandbox's counter of this kind of tank.</summary>
        protected abstract void Count(int delta);

        /// <summary>A frame of the AI: drive (<see cref="DriveTo"/>, <see cref="Hold"/>), aim and shoot.</summary>
        protected abstract void Think();

        private void OnDestroy() => Uncount();

        private void OnDisable() => Uncount();

        private void Uncount()
        {
            if (!_counted)
                return;
            _counted = false;
            Count(-1);
        }

        private void Update()
        {
            if (Entity == null || !Entity.TryGetHealth(out Health health) || health.IsEmpty())
            {
                if (_drive != null)
                    _drive.Value = Vector2.zero;
                enabled = false;
                return;
            }

            Player.Refresh();
            Think();
        }

        protected Vector3 GunPosition => Pivot != null ? Pivot.position : transform.position + Vector3.up * AIM_HEIGHT;

        protected void AimAt(Vector3 point)
        {
            Vector3 dir = point - GunPosition;
            if (dir.sqrMagnitude > 0.01f)
                _aim.Value = dir.normalized;
        }

        protected void AimAlong(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.01f)
                _aim.Value = direction.normalized;
        }

        /// <summary>Stands still, the hull turned to <paramref name="face"/> (flat); zero — just stands.</summary>
        protected void Hold(Vector3 face)
        {
            if (Backing())
                return;
            face.y = 0f;
            float turn = face.sqrMagnitude > 0.01f ? Turn(face) : 0f;
            // Small turns are left alone: the tank does not twitch on the spot.
            _drive.Value = new Vector2(Mathf.Abs(turn) < 0.15f ? 0f : turn, 0f);
            SetBoost(false);
            _stuck = 0f;
        }

        /// <summary>
        /// Drives along <paramref name="flat"/> (an offset on the ground) with <paramref name="throttle"/> (−1 back … 1
        /// ahead). Ahead it turns first when the goal is well off the nose; boost — Shift held.
        /// </summary>
        protected void DriveTo(Vector3 flat, float throttle, bool boost = false)
        {
            if (Backing())
                return;

            flat.y = 0f;
            float angle = Vector3.SignedAngle(Forward, flat, Vector3.up);
            float turn = Mathf.Clamp(angle / FULL_TURN, -1f, 1f);
            if (throttle > 0f)
                // Ahead at full only when roughly facing it; turn on the spot when it is behind.
                throttle *= Mathf.Abs(angle) < 60f ? 1f : Mathf.Abs(angle) < 110f ? 0.3f : 0f;
            _drive.Value = new Vector2(turn, throttle);
            SetBoost(boost && Mathf.Abs(angle) < 30f);

            // Throttling and not moving: up against a wall or a house. Back off turning away, then try again.
            float speed = Vector3.Dot(Body.linearVelocity, Forward);
            if (Time.time > _startedAt + CRANK_TIME && Mathf.Abs(throttle) > 0.5f && Mathf.Abs(speed) < STUCK_SPEED)
            {
                _stuck += Time.deltaTime;
                if (_stuck > STUCK_TIME)
                {
                    _stuck = 0f;
                    _backup = BACKUP_TIME;
                    _backupTurn = Random.value < 0.5f ? -1f : 1f;
                }
            }
            else
                _stuck = 0f;
        }

        /// <summary>
        /// Fires at <paramref name="target"/> when the turret is on it, the line is not <see cref="LineOfFire.Blocked"/>
        /// and its own pause is over. True — fired.
        /// </summary>
        protected bool TryFire(Vector3 target, LineOfFire line)
        {
            if (Time.time < _nextShot || Pivot == null || line == LineOfFire.Blocked)
                return false;

            Vector3 barrel = Pivot.forward;
            Vector3 want = target - Pivot.position;
            barrel.y = 0f;
            want.y = 0f;
            if (Vector3.Angle(barrel, want) > FIRE_ANGLE)
                return false;

            if (!Entity.GetFireCondition().Invoke())
                return false;

            Entity.GetFireAction().Invoke();
            Vector2 pause = FirePause;
            _nextShot = Time.time + Random.Range(pause.x, pause.y);
            return true;
        }

        /// <summary>
        /// What lies between the gun and <paramref name="to"/>, past this tank, loose debris and <paramref name="through"/>
        /// (the target's own body). Voxel walls are no cover — a shell goes through them or blows them open; anything
        /// else solid blocks, <paramref name="block"/> (a friend in the way) too.
        /// </summary>
        protected LineOfFire Line(Vector3 to, Transform through, Transform block = null)
        {
            Vector3 from = GunPosition;
            Vector3 dir = to - from;
            float length = dir.magnitude;
            if (length < 0.01f)
                return LineOfFire.Clear;
            int count = Physics.RaycastNonAlloc(from, dir / length, s_hits, length, ~0, QueryTriggerInteraction.Ignore);
            LineOfFire line = LineOfFire.Clear;
            for (int i = 0; i < count; i++)
            {
                Transform hit = s_hits[i].transform;
                if (block != null && hit.IsChildOf(block))
                    return LineOfFire.Blocked;
                if (hit.IsChildOf(transform) || (through != null && hit.IsChildOf(through)))
                    continue;
                if (s_hits[i].rigidbody != null && !s_hits[i].rigidbody.isKinematic)
                {
                    // Another tank is solid; loose debris is not.
                    if (s_hits[i].rigidbody.GetComponent<TankAIBrain>() != null)
                        return LineOfFire.Blocked;
                    continue;
                }
                if (!Game.Voxels.VoxelDamage.IsVoxel(s_hits[i].collider))
                    return LineOfFire.Blocked;
                line = LineOfFire.ThroughVoxels;
            }
            return line;
        }

        protected Vector3 Forward
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            }
        }

        protected static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private bool Backing()
        {
            if (_backup <= 0f)
                return false;
            _backup -= Time.deltaTime;
            _drive.Value = new Vector2(_backupTurn, -1f);
            SetBoost(false);
            return true;
        }

        private void SetBoost(bool on)
        {
            if (_boost != null && _boost.Value != on)
                _boost.Value = on;
        }

        private float Turn(Vector3 flat) => Mathf.Clamp(Vector3.SignedAngle(Forward, flat, Vector3.up) / FULL_TURN, -1f, 1f);
    }
}
