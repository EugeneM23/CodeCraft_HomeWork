using Atomic.Elements;
using Atomic.Entities;
using HighlightPlus;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// A helicopter the player can fly. Like the tank, it carries the keys the player controllers
    /// read off a character — RotateDirection (where the nose turns), Weapon, FireCondition,
    /// FireAction, CameraPoint, Health — so once PlayerContext.Character points at it, aiming,
    /// firing, the camera and its shake work unchanged. The stick is DriveInput
    /// (ControllableDriveController), read here as a direction on the ground, as on foot.
    ///
    /// Getting in starts the take-off; the exit key in the air starts the landing, and the pilot
    /// is put out on touchdown.
    ///
    /// Every collider sits on this object: Collider.TryGetEntity only looks at the collider's own
    /// GameObject.
    /// </summary>
    public sealed class HelicopterInstaller : MonoEntityInstaller
    {
        [Header("Flight")]
        [Tooltip("Height (m) of the skids above the ground while flying.")]
        [SerializeField] private float _cruiseHeight = 5f;
        [SerializeField] private float _moveSpeed = 10f;
        [Tooltip("Shift held: top speed and acceleration are multiplied by this.")]
        [SerializeField, Min(1f)] private float _boostMultiplier = 1.8f;
        [Tooltip("m/s² gained while speeding up.")]
        [SerializeField] private float _acceleration = 7f;
        [Tooltip("m/s² shed while slowing down. Lower than the acceleration: the helicopter drifts on.")]
        [SerializeField] private float _braking = 5f;
        [Tooltip("Vertical speed (m/s) wanted per metre still to climb or descend.")]
        [SerializeField] private float _climbGain = 3f;
        [SerializeField] private float _maxClimbSpeed = 3f;
        [SerializeField] private float _verticalAcceleration = 6f;
        [Tooltip("Degrees per second the nose turns at most.")]
        [SerializeField] private float _turnSpeed = 120f;
        [Tooltip("Degrees per second² the turn rate ramps at.")]
        [SerializeField] private float _turnAcceleration = 360f;

        [Header("Tilt (visual)")]
        [SerializeField] private float _tiltPerAcceleration = 2f;
        [SerializeField] private float _tiltPerSpeed = 0.4f;
        [SerializeField] private float _bankPerTurnRate = 0.05f;
        [SerializeField] private float _maxTilt = 15f;
        [SerializeField] private float _tiltFrequency = 2f;
        [SerializeField, Range(0f, 1.5f)] private float _tiltDamping = 0.5f;

        [Header("Rotors (visual)")]
        [SerializeField] private Transform _mainRotor;
        [SerializeField] private Transform _tailRotor;
        [SerializeField] private float _mainRotorSpeed = 900f;
        [SerializeField] private float _tailRotorSpeed = 2000f;
        [SerializeField] private float _spinUpTime = 1.5f;
        [SerializeField] private float _spinDownTime = 3f;

        [Header("Guns")]
        [Tooltip("Fired together, one round each time a barrel comes round. The first one is the Weapon the camera shakes for.")]
        [SerializeField] private MonoEntity[] _guns;
        [Tooltip("Level mounts the guns and their fire points hang from, in the same order.")]
        [SerializeField] private Transform[] _gunPivots;
        [Tooltip("The visible guns, inside the leaning body, each copying its pivot's turn. Same order.")]
        [SerializeField] private Transform[] _gunVisuals;
        [SerializeField] private float _gunTurnSpeed = 180f;
        [SerializeField] private float _gunMaxYaw = 30f;
        [SerializeField] private float _gunMaxPitchDown = 70f;

        [Header("Minigun")]
        [Tooltip("The barrel cluster, spun about its forward. Inside the gun visual.")]
        [SerializeField] private Transform _minigunBarrels;
        [SerializeField, Min(1)] private int _barrelCount = 8;
        [Tooltip("Degrees per second at full speed. A round leaves every 360 / barrels degrees: 1440 with 8 barrels is 32 rounds a second.")]
        [SerializeField] private float _minigunMaxSpin = 1440f;
        [SerializeField] private float _minigunSpinUpTime = 0.6f;
        [SerializeField] private float _minigunSpinDownTime = 1.6f;
        [Tooltip("Share of full speed below which the barrels spin without firing.")]
        [SerializeField, Range(0.01f, 1f)] private float _minigunFireThrottle = 0.3f;

        [Header("Body")]
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private Transform _bodyVisual;
        [SerializeField] private Transform _cameraPoint;
        [SerializeField] private float _cameraDistance = 1.15f;
        [Tooltip("How much of the helicopter's height the camera climbs with it: 0 stays at the on-foot height, 1 climbs all of it.")]
        [SerializeField, Range(0f, 1f)] private float _cameraHeightShare = 0.2f;
        [SerializeField] private Transform[] _exitPoints;
        [SerializeField] private int _health = 250;

        [Header("Interaction")]
        [SerializeField] private HighlightEffect _highlight;
        [SerializeField] private Vector3 _markerOffset = new(0f, 2.6f, 0f);

        [Header("Destruction")]
        [SerializeField] private MonoEntity _explosionVfx;
        [SerializeField] private CameraShakeArgs _destroyShake;

        public override void Install(IEntity entity)
        {
            GameContext gameContext = GameContext.Instance;
            float ground = transform.position.y;

            // Core
            entity.AddVehicleTag();
            entity.AddDamageableTag();
            entity.AddInteractableTag();
            entity.AddGameObject(gameObject);
            entity.AddTransform(transform);
            entity.AddRiggedBody(_rigidbody);

            // Health
            HealthUseCase.AddHealthWithEvents(entity, _health);

            // Flight
            entity.AddDriver(new ReactiveVariable<IEntity>());
            entity.AddDriveInput(new ReactiveVariable<Vector2>());
            entity.AddDriveBoost(new ReactiveVariable<bool>());
            entity.AddMoveSpeed(new ReactiveFloat(_moveSpeed));
            entity.AddRotationSpeed(new Const<float>(_turnSpeed));
            entity.AddExitPoints(_exitPoints);
            entity.AddFlightState(new ReactiveVariable<HelicopterFlightState>(HelicopterFlightState.Landed));

            // Aim: the nose follows the cursor on the ground, the gun dips to it.
            entity.AddRotateDirection(new ReactiveVariable<Vector3>(transform.forward));
            entity.AddAimHeight(new Const<float>(ground));
            entity.AddAimPoint(new ReactiveVariable<Vector3>(transform.position + transform.forward * 10f));

            // Combat: each gun is a weapon entity of its own, fired by the minigun's barrels — the
            // button only pulls the trigger, the spin sets the rate. Only with somebody inside and
            // the hull intact. Bullets fly through the helicopter's own colliders.
            Collider[] hull = GetComponents<Collider>();
            foreach (MonoEntity gun in _guns)
                if (gun != null)
                    gun.SetShooterColliders(hull);

            MinigunBehaviour minigun = new(_minigunBarrels, _guns, _barrelCount, _minigunMaxSpin,
                _minigunSpinUpTime, _minigunSpinDownTime, _minigunFireThrottle);

            entity.AddWeapon(new ReactiveVariable<IEntity>(_guns.Length > 0 ? _guns[0] : null));
            entity.AddFireCondition(new AndExpression(
                new ExpressionMember<bool>(entity.GetHealth().Exists),
                new ExpressionMember<bool>(() => entity.GetDriver().Value != null)
            ));
            entity.AddFireAction(new InlineAction(minigun.Pull));

            // Camera and visuals. HullVisual is what leans, and what the wreck darkens.
            entity.AddCameraPoint(_cameraPoint);
            entity.AddCameraDistance(_cameraDistance);
            entity.AddHullVisual(_bodyVisual);

            // Interaction
            if (_highlight != null)
            {
                ControllableHighlight.UseSingleTargetFX(_highlight);
                entity.AddHighlight(_highlight);
            }
            entity.AddPositionOffset(_markerOffset);

            entity.AddInteractAction(new InlineAction<IEntity>(driver =>
            {
                if (ControllableUseCase.CanEnter(entity, driver))
                    ControllableUseCase.Enter(entity, driver, gameContext.GetPlayerContext().GetCharacter(),
                        gameContext.GetEntityWorld());
            }));

            void TryExit() => ControllableUseCase.TryExit(entity, gameContext.GetPlayerContext().GetCharacter(),
                gameContext.GetEntityWorld());

            HelicopterFlightBehaviour flight = new(ground, _cruiseHeight, _acceleration, _braking, _climbGain,
                _maxClimbSpeed, _verticalAcceleration, onTouchdown: TryExit, boostMultiplier: _boostMultiplier);

            // In the air the exit key starts the landing and the pilot gets out on touchdown. On the
            // ground — an exit refused on touchdown — it tries again at once.
            entity.AddVehicleExitAction(new InlineAction(() =>
            {
                if (entity.GetFlightState().Value == HelicopterFlightState.Landed)
                    TryExit();
                else
                    flight.RequestLanding();
            }));

            // Behaviours. The heading goes before the flight: the tilt reads both in the same step.
            entity.AddBehaviour(new HelicopterYawBehaviour(_turnAcceleration));
            entity.AddBehaviour(flight);
            entity.AddBehaviour(new HelicopterTiltBehaviour(_tiltPerAcceleration, _tiltPerSpeed, _bankPerTurnRate,
                _maxTilt, _tiltFrequency, _tiltDamping));
            entity.AddBehaviour(new HelicopterGunAimBehaviour(_gunPivots, _gunVisuals, _gunTurnSpeed, _gunMaxYaw,
                _gunMaxPitchDown));
            entity.AddBehaviour(minigun);
            entity.AddBehaviour(new HelicopterCameraPointBehaviour(_cameraPoint, ground, _cameraHeightShare));
            entity.AddBehaviour(new RotorSpinBehaviour(_mainRotor, _tailRotor, _mainRotorSpeed, _tailRotorSpeed,
                _spinUpTime, _spinDownTime));
            entity.AddBehaviour(new TankDestroyBehaviour(gameContext, _explosionVfx, _destroyShake, fallOnDeath: true));
            entity.AddBehaviour(new DriverAudioListenerBehaviour());
        }
    }
}
