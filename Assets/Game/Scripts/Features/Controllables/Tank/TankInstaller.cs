using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The player's tank. It carries the keys the player controllers read off the character in
    /// PlayerContext.Character — RotateDirection (the turret's aim), Weapon, FireCondition,
    /// FireAction, CameraPoint, Health — and its own drive stick, DriveInput, written by
    /// ControllableDriveController. It runs and fires while it has a Driver
    /// (PlayerVehicleController gives it the player).
    ///
    /// Every collider sits on this object. Collider.TryGetEntity only looks at the collider's own
    /// GameObject, so a collider on a child would leave bullets, blasts and enemy swings unable
    /// to find the tank.
    /// </summary>
    public sealed class TankInstaller : MonoEntityInstaller
    {
        [Header("Drive")]
        [SerializeField] private float _moveSpeed = 6f;
        [Tooltip("Degrees per second the hull turns at full stick.")]
        [SerializeField] private float _turnSpeed = 78f;
        [Tooltip("m/s² gained while speeding up in the direction already travelled.")]
        [SerializeField] private float _acceleration = 6f;
        [Tooltip("m/s² shed when letting off or reversing.")]
        [SerializeField] private float _braking = 12f;
        [Tooltip("Degrees per second² the turn rate ramps at.")]
        [SerializeField] private float _turnAcceleration = 360f;
        [Tooltip("How hard the hull squats pulling away and dips braking: 1 is what the push at the tracks gives by itself, 2 twice that.")]
        [SerializeField, Min(0f)] private float _pitchLean = 2f;
        [Tooltip("Shift held: engine power. Top speed, turn rate and how fast each is reached are all multiplied by this; braking is not.")]
        [SerializeField, Min(1f)] private float _boostMultiplier = 1.8f;
        [Tooltip("m/s along the hull below which the tracks never drift. Above the normal top speed: only Shift drifts.")]
        [SerializeField, Min(0f)] private float _driftSpeed = 7.5f;
        [Tooltip("Above the drift speed: m/s² of sideways slide the tracks can take out. A turn asking more (speed × turn rate) slides out by the excess. Lower drifts more.")]
        [SerializeField, Min(0f)] private float _sideGrip = 18f;

        [Tooltip("Emitters at the exhaust pipes' mouths: black smoke, the jet growing with speed.")]
        [SerializeField] private ParticleSystem[] _exhaust;
        [Tooltip("Experiment: the exhaust smoke as voxel cubes; off brings the particle smoke back.")]
        [SerializeField] private VoxelExhaustSettings _voxelExhaust = new();

        [Header("Turret")]
        [SerializeField] private Transform _turretPivot;
        [SerializeField] private Transform _turretVisual;
        [Tooltip("Degrees per second.")]
        [SerializeField] private float _turretSpeed = 90f;
        [Tooltip("Main gun, left mouse button.")]
        [SerializeField] private MonoEntity _cannon;
        [Tooltip("Turret machine gun, right mouse button.")]
        [SerializeField] private MonoEntity _machineGun;
        [Tooltip("The visible barrel group that recoils on a shot, sliding along its local Z.")]
        [SerializeField] private Transform _gunVisual;
        [Tooltip("How far (m) the barrel runs back on a shot.")]
        [SerializeField] private float _gunRecoilDistance = 0.6f;
        [SerializeField] private float _gunKickTime = 0.05f;
        [Tooltip("Seconds the barrel stays fully back before running out again.")]
        [SerializeField] private float _gunHoldTime = 0.12f;
        [Tooltip("Seconds the recuperator takes to run the barrel back into battery.")]
        [SerializeField] private float _gunReturnTime = 0.8f;
        [Tooltip("Most the visible gun nods on its trunnion (degrees) as the hull rocks.")]
        [SerializeField, Min(0f)] private float _gunSwayLimit = 12f;
        [Tooltip("The gun's own swing on the trunnion (Hz).")]
        [SerializeField, Min(0.1f)] private float _gunSwayFrequency = 1.4f;
        [Tooltip("How fast the swing dies: 1 — no overshoot.")]
        [SerializeField, Range(0f, 1.5f)] private float _gunSwayDamping = 0.14f;
        [Tooltip("Share of the hull's change of pitch the gun lags behind (0 — none, 1 — all).")]
        [SerializeField, Range(0f, 1f)] private float _gunSwayInertia = 0.35f;
        [Tooltip("Barrel tip per vertical jolt (rad per m/s² of the trunnion's acceleration).")]
        [SerializeField, Min(0f)] private float _gunSwayHeave = 0.004f;
        [Tooltip("Nod per m/s² along the barrel (rad): up when speeding up, down when braking.")]
        [SerializeField, Min(0f)] private float _gunSwaySurge = 0.015f;
        [Tooltip("Muzzle flip on a shot: the gun's upward swing speed (degrees/s) the kick gives it.")]
        [SerializeField, Min(0f)] private float _gunSwayShotKick = 60f;
        [Tooltip("Pooled burst (HitEffectInstaller) put at the cannon's muzzle on every shot: flash, flame, smoke.")]
        [SerializeField] private MonoEntity _muzzleSmoke;
        [Tooltip("Smoke still trickling out of the barrel after a shot; restarted on every shot.")]
        [SerializeField] private ParticleSystem _muzzleWisp;
        [Tooltip("Experiment: voxel cubes as the cannon's smoke; switches for the old particle smoke.")]
        [SerializeField] private VoxelMuzzleSmokeSettings _voxelMuzzleSmoke = new();

        [Header("Body")]
        [SerializeField] private Rigidbody _rigidbody;
        [Tooltip("The hull's box (not the turret's): swept ahead for ground the nose is about to plough into.")]
        [SerializeField] private BoxCollider _hullCollider;
        [SerializeField] private Transform _hullVisual;
        [SerializeField] private Transform _cameraPoint;
        [Tooltip("Camera offset multiplier while driving: 1.1 sits the camera a tenth further out.")]
        [SerializeField] private float _cameraDistance = 1.1f;
        [SerializeField] private int _health = 1000;

        [Header("Suspension")]
        [Tooltip("Where each road wheel's spring hangs from, on the hull. The ray starts here.")]
        [SerializeField] private Transform[] _wheelMounts;
        [Tooltip("The visible road wheels, in the same order as the mounts.")]
        [SerializeField] private Transform[] _wheelVisuals;
        [SerializeField] private float _wheelRadius = 0.225f;
        [Tooltip("Spring hanging free, mount to wheel centre.")]
        [SerializeField] private float _springMaxLength = 0.41f;
        [Tooltip("The bump stop, mount to wheel centre.")]
        [SerializeField] private float _springMinLength = 0.19f;
        [Tooltip("Share of the travel the hull sinks under its own weight. The stiffness follows from it and the mass.")]
        [SerializeField, Range(0.1f, 0.9f)] private float _staticSag = 0.5f;
        [Tooltip("1 settles without overshoot; lower rocks a little before it settles.")]
        [SerializeField, Range(0f, 1.5f)] private float _dampingRatio = 0.4f;
        [Tooltip("Impulse (N·s) the cannon kicks the hull back with, at the muzzle.")]
        [SerializeField] private float _recoilImpulse = 12000f;
        [SerializeField] private TankTrackRig _leftTrack;
        [SerializeField] private TankTrackRig _rightTrack;

        [Header("Plough")]
        [Tooltip("How hard the nose ploughs into ground it hits at speed; the sandbox switches between them (M).")]
        [SerializeField] private TankPloughConfig[] _ploughPresets;
        [SerializeField, Min(0)] private int _ploughPreset = 1;

        [Header("Crushing")]
        [Tooltip("Reports the hull's contacts, lasting ones included. Without it the tank crushes nothing it drives into.")]
        [SerializeField] private CollisionContactReceiver _contacts;
        [Tooltip("Enemies the tank runs into fast enough die under it: blood from under the tracks, splashes on the skin.")]
        [SerializeField] private TankCrushConfig _crush = new();

        [Header("Destruction")]
        [SerializeField] private MonoEntity _explosionVfx;
        [SerializeField] private CameraShakeArgs _destroyShake;

        private TankPloughBehaviour _plough;

        /// <summary>The main gun. For CombatPoolPrewarm: the muzzle smoke comes as often as it fires.</summary>
        public MonoEntity Cannon => _cannon;

        /// <summary>
        /// The middle of the turret's own meshes (not the gun's), in the hull's frame: the third-person camera orbits it.
        /// The turret pivot's spot when there are no meshes.
        /// </summary>
        public Vector3 TurretCentreLocal()
        {
            Transform turret = _turretVisual != null ? _turretVisual : _turretPivot;
            if (turret == null)
                return Vector3.up * 2f;
            Bounds bounds = default;
            bool any = false;
            // The meshes, not the renderers: a renderer switched off (the voxel skin replaces them) has empty bounds.
            foreach (MeshFilter filter in turret.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || (_gunVisual != null && filter.transform.IsChildOf(_gunVisual)))
                    continue;
                Bounds local = filter.sharedMesh.bounds;
                Vector3 c = local.center;
                Vector3 e = local.extents;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = filter.transform.TransformPoint(c + new Vector3(
                        (i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
                    if (!any)
                    {
                        bounds = new Bounds(corner, Vector3.zero);
                        any = true;
                    }
                    else
                        bounds.Encapsulate(corner);
                }
            }
            return transform.InverseTransformPoint(any ? bounds.center : turret.position);
        }

        /// <summary>
        /// Pooled particle burst put at the muzzle on every shot of the cannon, or null while it is switched off
        /// (Muzzle Smoke (voxel) → Particle Burst; the voxel fire and smoke replace it). For CombatPoolPrewarm.
        /// </summary>
        public MonoEntity MuzzleSmoke => _voxelMuzzleSmoke == null || _voxelMuzzleSmoke.ParticleBurst ? _muzzleSmoke : null;

        /// <summary>Pooled blast the tank goes up in. For CombatPoolPrewarm.</summary>
        public MonoEntity ExplosionVfx => _explosionVfx;

        /// <summary>The plough preset in use, or null without one.</summary>
        public TankPloughConfig PloughPreset =>
            _ploughPresets != null && _ploughPresets.Length > 0 ? _ploughPresets[_ploughPreset % _ploughPresets.Length] : null;

        /// <summary>The nose's ploughing, or null without a hull collider or a preset. For tests.</summary>
        public TankPloughBehaviour Plough => _plough;

        /// <summary>The next plough preset, round and round. For the sandbox.</summary>
        public void NextPloughPreset()
        {
            if (_ploughPresets == null || _ploughPresets.Length == 0)
                return;
            _ploughPreset = (_ploughPreset + 1) % _ploughPresets.Length;
            if (_plough != null)
                _plough.Config = PloughPreset;
        }

        public override void Install(IEntity entity)
        {
            GameContext gameContext = GameContext.Instance;

            // Core
            entity.AddVehicleTag();
            entity.AddDamageableTag();
            entity.AddGameObject(gameObject);
            entity.AddTransform(transform);
            entity.AddRiggedBody(_rigidbody);

            // Health
            HealthUseCase.AddHealthWithEvents(entity, _health);

            // Drive
            entity.AddDriver(new ReactiveVariable<IEntity>());
            entity.AddDriveInput(new ReactiveVariable<Vector2>());
            entity.AddDriveBoost(new ReactiveVariable<bool>());
            entity.AddMoveSpeed(new ReactiveFloat(_moveSpeed));
            entity.AddRotationSpeed(new Const<float>(_turnSpeed));
            entity.AddSuspension(new TankSuspension(_wheelMounts, _wheelVisuals, _wheelRadius,
                _springMaxLength, _springMinLength, _staticSag, _dampingRatio));

            // Turret
            entity.AddRotateDirection(new ReactiveVariable<Vector3>(transform.forward));
            entity.AddTurretPivot(_turretPivot);
            if (_turretVisual != null)
                entity.AddTurretVisual(_turretVisual);
            // A variable, not a constant: the loadout (LoadoutApplyUseCase) sets it.
            entity.AddTurretRotationSpeed(new ReactiveFloat(_turretSpeed));

            // Combat: the cannon is a weapon entity of its own; the tank fires it only while
            // somebody drives it and the hull is intact.
            entity.AddWeapon(new ReactiveVariable<IEntity>(_cannon));
            entity.AddFireCondition(new AndExpression(
                new ExpressionMember<bool>(entity.GetHealth().Exists),
                new ExpressionMember<bool>(() => entity.GetDriver().Value != null),
                new ExpressionMember<bool>(() => _cannon.GetFireCondition().Invoke())
            ));
            entity.AddFireAction(new InlineAction(() => _cannon.GetFireAction().Invoke()));

            if (_machineGun != null)
            {
                entity.AddSecondaryWeapon(new ReactiveVariable<IEntity>(_machineGun));
                entity.AddSecondaryFireCondition(new AndExpression(
                    new ExpressionMember<bool>(entity.GetHealth().Exists),
                    new ExpressionMember<bool>(() => entity.GetDriver().Value != null),
                    new ExpressionMember<bool>(() => _machineGun.GetFireCondition().Invoke())
                ));
                entity.AddSecondaryFireAction(new InlineAction(() => _machineGun.GetFireAction().Invoke()));
            }

            // Camera and visuals
            entity.AddCameraPoint(_cameraPoint);
            entity.AddCameraDistance(_cameraDistance);
            entity.AddHullVisual(_hullVisual);

            // Behaviours. The suspension goes first: the drive reads what it found under the wheels
            // in the same physics step.
            // The engine cranks for two seconds after it gets a driver; the drive waits for it.
            TankEngine engine = new();
            entity.AddBehaviour(engine);
            entity.AddBehaviour<TankSuspensionBehaviour>();
            entity.AddBehaviour(new TankDriveBehaviour(_acceleration, _braking, _turnAcceleration, _pitchLean,
                _boostMultiplier, engine, _driftSpeed, _sideGrip));
            // After the drive: the nose rips a chunk out of ground it is about to hit, and the hull's
            // bounce off it is set here, over the drive's push.
            if (_hullCollider != null && PloughPreset != null)
            {
                _plough = new TankPloughBehaviour(_hullCollider, PloughPreset, new DebrisEarthThrow());
                entity.AddBehaviour(_plough);
            }
            if (_contacts != null)
            {
                // Enemies it runs into fast enough die under it, in blood (TankCrushBehaviour).
                entity.AddBehaviour(new TankCrushBehaviour(_contacts, _rigidbody, _crush));
            }
            // Debris and voxel flesh on the ground no longer touch the hull.
            entity.AddBehaviour(new PassThroughLoosePiecesBehaviour());
            // Voxel flesh the tracks run over bursts into goo (TankSquashBehaviour).
            entity.AddBehaviour(new TankSquashBehaviour(_rigidbody));
            // Death goo and burst body parts bloody the skin where they hit (TankBloodBehaviour).
            entity.AddBehaviour(new TankBloodBehaviour(_rigidbody));
            entity.AddBehaviour<TurretAimBehaviour>();
            entity.AddBehaviour(new TankRecoilBehaviour(_recoilImpulse));
            if (_gunVisual != null)
            {
                entity.AddBehaviour(new GunRecoilBehaviour(_gunVisual, _gunRecoilDistance, _gunKickTime, _gunHoldTime,
                    _gunReturnTime));
                // The gun nods on its trunnion with the hull's rocking (visual only; the fire point stays level).
                if (_gunSwayLimit > 0f)
                    entity.AddBehaviour(new TankGunSwayBehaviour(_gunVisual, _gunSwayLimit, _gunSwayFrequency,
                        _gunSwayDamping, _gunSwayInertia, _gunSwayHeave, _gunSwaySurge, _gunSwayShotKick));
            }
            entity.AddBehaviour(new TankMuzzleSmokeBehaviour(gameContext, _muzzleSmoke, _muzzleWisp, _voxelMuzzleSmoke));
            entity.AddBehaviour(new TankVoxelMuzzleSmokeBehaviour(_voxelMuzzleSmoke));
            // Voxel flame, sparks and a flash at the muzzle on every shot, in front of the voxel smoke.
            entity.AddBehaviour(new MuzzleFireBehaviour(true));
            if (_hullCollider != null)
                entity.AddBehaviour(new TankSmokeHullBehaviour(_hullCollider, _rigidbody));
            // Shift on the gas: the tracks spin and throw earth back; full gas rams harder.
            TankWheelSpinBehaviour wheelSpin = new(engine, _boostMultiplier);
            entity.AddBehaviour(wheelSpin);
            entity.AddBehaviour(new TankTrackView(_leftTrack, _rightTrack, wheelSpin));
            // After the view: the ruts go where the wheels were just drawn.
            TankDirtBehaviour dirt = new();
            entity.AddBehaviour(dirt);
            entity.AddBehaviour(new TankTrackMarksBehaviour(_leftTrack, _rightTrack, dirt));
            entity.AddBehaviour(new TankMuzzleMudBehaviour(dirt));
            if (_exhaust != null && _exhaust.Length > 0)
                entity.AddBehaviour(new TankExhaustBehaviour(_exhaust, engine, _acceleration, _boostMultiplier,
                    _voxelExhaust));
            entity.AddBehaviour(new TankDestroyBehaviour(gameContext, _explosionVfx, _destroyShake));
            entity.AddBehaviour(new DriverAudioListenerBehaviour());
        }
    }
}
