using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Prototype (the user): a copy of the player's tank driven by the AI. It writes the same keys the player's controllers
    /// write — Driver, DriveInput, RotateDirection — and pulls FireAction, so the tank drives, aims and shoots with all its
    /// own physics, recoil and effects. It closes to a fighting range, holds there turning its hull to us, backs off when
    /// we come too close, and shoots once the turret is on us and nothing blocks the line. Stuck against something: it
    /// backs off turning, then tries again.
    /// </summary>
    public sealed class EnemyTankBrain : MonoBehaviour
    {
        private const float FAR = 24f;          // drives up while further than this, m
        private const float NEAR = 12f;         // backs off while nearer than this, m
        private const float FIRE_RANGE = 55f;   // shoots inside this, m
        private const float FIRE_ANGLE = 4f;    // the turret is this close to the aim, degrees
        private const float FULL_TURN = 35f;    // hull angle off that gives a full stick, degrees
        private const float STUCK_SPEED = 0.6f; // under this while throttling it is stuck, m/s
        private const float STUCK_TIME = 1.5f;
        private const float BACKUP_TIME = 1.6f;
        private const float AIM_HEIGHT = 1.2f;
        private const float FIRE_PAUSE_MIN = 1.2f; // its own pause between shots on top of the reload, s
        private const float FIRE_PAUSE_MAX = 3f;

        private static readonly RaycastHit[] s_hits = new RaycastHit[8];

        private MonoEntity _entity;
        private Rigidbody _body;
        private Transform _pivot;
        private IReactiveVariable<Vector2> _drive;
        private IReactiveVariable<Vector3> _aim;
        private readonly PlayerTank _player = new();

        private float _stuck;
        private float _backup;
        private float _backupTurn;
        private float _nextShot;
        private float _startedAt;
        private LineOfFire _line = LineOfFire.Clear;

        private enum LineOfFire { Clear, ThroughVoxels, Blocked }

        /// <summary>Spawned and alive enemy tanks (for the sandbox's counter).</summary>
        public static int Alive { get; private set; }

        private bool _counted;

        private void Start()
        {
            _entity = GetComponent<MonoEntity>();
            _body = GetComponent<Rigidbody>();
            _drive = _entity.GetDriveInput();
            _aim = _entity.GetRotateDirection();
            _pivot = _entity.GetTurretPivot();

            // It is ours to drive; the player's controllers never see it (it is not the player's character).
            _entity.GetDriver().Value = _entity;
            _entity.AddEnemyTag();

            // One ear in the scene: the player's tank. The driver behaviour made one here too.
            if (TryGetComponent(out AudioListener ear))
                Destroy(ear);

            // Armour-piercing: an HE blast hurts only monsters (ExplosionUseCase), so HE would never scratch us.
            if (_entity.TryGetWeapon(out IReactiveVariable<IEntity> cannon) && cannon.Value != null)
                AmmoUseCase.GetOrAdd(cannon.Value).Value = ShellKind.ArmourPiercing;

            Alive++;
            _counted = true;
            _startedAt = Time.time;
            _nextShot = Time.time + 3f; // the engine cranks first
        }

        private void OnDestroy() => Uncount();

        private void OnDisable() => Uncount();

        private void Uncount()
        {
            if (!_counted)
                return;
            _counted = false;
            Alive--;
        }

        private void Update()
        {
            if (_entity == null || !_entity.TryGetHealth(out Health health) || health.IsEmpty())
            {
                if (_drive != null)
                    _drive.Value = Vector2.zero;
                enabled = false;
                return;
            }

            _player.Refresh();
            if (!_player.IsPresent)
            {
                _drive.Value = Vector2.zero;
                return;
            }

            Vector3 target = _player.AimPoint;
            Vector3 me = transform.position;
            Vector3 flat = target - me;
            flat.y = 0f;
            float distance = flat.magnitude;

            Aim(target);
            // The line of fire, every few frames.
            if (Time.frameCount % 6 == 0 && _pivot != null)
                _line = Line(_pivot.position, target);
            Drive(flat, distance);
            Fire(target, distance);
        }

        private void Aim(Vector3 target)
        {
            Vector3 from = _pivot != null ? _pivot.position : transform.position + Vector3.up * AIM_HEIGHT;
            Vector3 dir = target - from;
            if (dir.sqrMagnitude > 0.01f)
                _aim.Value = dir.normalized;
        }

        private void Drive(Vector3 flat, float distance)
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            float speed = Vector3.Dot(_body.linearVelocity, forward.normalized);

            if (_backup > 0f)
            {
                _backup -= Time.deltaTime;
                _drive.Value = new Vector2(_backupTurn, -1f);
                return;
            }

            float angle = Vector3.SignedAngle(forward, flat, Vector3.up);
            float turn = Mathf.Clamp(angle / FULL_TURN, -1f, 1f);
            float throttle;
            // Nothing to shoot through from here (a ramp, a rock): come on in till there is.
            if (distance > FAR || (_line == LineOfFire.Blocked && distance > NEAR))
                // Ahead at full only when roughly facing it; turn on the spot when it is behind.
                throttle = Mathf.Abs(angle) < 60f ? 1f : Mathf.Abs(angle) < 110f ? 0.3f : 0f;
            else if (distance < NEAR)
                throttle = -0.7f;
            else
                throttle = 0f;

            _drive.Value = new Vector2(turn, throttle);

            // Throttling and not moving: up against a wall or a house. Back off turning away, then try again.
            // Not while the engine still cranks (two seconds after it got its driver).
            if (Time.time > _startedAt + 3f && Mathf.Abs(throttle) > 0.5f && Mathf.Abs(speed) < STUCK_SPEED)
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

        private void Fire(Vector3 target, float distance)
        {
            if (distance > FIRE_RANGE || Time.time < _nextShot || _pivot == null)
                return;

            Vector3 barrel = _pivot.forward;
            Vector3 want = target - _pivot.position;
            barrel.y = 0f;
            want.y = 0f;
            if (Vector3.Angle(barrel, want) > FIRE_ANGLE)
                return;

            // Voxel walls in the way are no cover: the shell goes through them or blows them open.
            if (_line == LineOfFire.Blocked)
                return;

            if (!_entity.GetFireCondition().Invoke())
                return;

            _entity.GetFireAction().Invoke();
            _nextShot = Time.time + Random.Range(FIRE_PAUSE_MIN, FIRE_PAUSE_MAX);
        }

        // What lies between the gun and the player, past the two tanks themselves and loose debris.
        private LineOfFire Line(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float length = dir.magnitude;
            int count = Physics.RaycastNonAlloc(from, dir / length, s_hits, length, ~0, QueryTriggerInteraction.Ignore);
            Transform player = _player.Transform;
            LineOfFire line = LineOfFire.Clear;
            for (int i = 0; i < count; i++)
            {
                Transform hit = s_hits[i].transform;
                if (hit.IsChildOf(transform) || (player != null && hit.IsChildOf(player)))
                    continue;
                if (s_hits[i].rigidbody != null && !s_hits[i].rigidbody.isKinematic)
                    continue;
                if (!Game.Voxels.VoxelDamage.IsVoxel(s_hits[i].collider))
                    return LineOfFire.Blocked;
                line = LineOfFire.ThroughVoxels;
            }
            return line;
        }
    }
}
