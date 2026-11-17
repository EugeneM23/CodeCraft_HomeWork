using Atomic.Elements;
using Atomic.Entities;
using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Shift on the gas: the tracks spin faster than the ground goes by and throw earth back from
    /// under them. Hardest pulling away, easing off as the tank nears its boosted top speed, but
    /// never quite gone while Shift is held. Purely a look: the drive's push is unchanged.
    ///
    /// Also says when the tank is on full gas (Shift and the stick all the way), which makes it
    /// ram voxel walls harder (<see cref="VoxelRammer.FullGas"/>), and when it is in the air (no
    /// wheel on the ground), which lets it fly on through a wall it jumps at (<see cref="VoxelRammer.Airborne"/>).
    /// </summary>
    public sealed class TankWheelSpinBehaviour : IEntityInit, IEntityTick
    {
        /// <summary>How fast (1/s) the spin builds when Shift goes down, and dies when it comes up.</summary>
        private const float RISE = 8f;
        private const float FALL = 4f;

        /// <summary>Spin left at the boosted top speed, as a share of the spin pulling away.</summary>
        private const float SPIN_AT_TOP = 0.35f;

        /// <summary>Stick past this counts as full gas.</summary>
        private const float FULL_STICK = 0.9f;

        private readonly TankEngine _engine;
        private readonly float _boostMultiplier;

        private Transform _hull;
        private Rigidbody _body;
        private TankSuspension _suspension;
        private IValue<Vector2> _input;
        private IValue<bool> _boost;
        private IValue<float> _maxSpeed;
        private IValue<IEntity> _driver;
        private VoxelTrackRuts _ruts;
        private VoxelRammer _rammer;

        /// <summary>0 — the tracks grip; 1 — they spin flat out.</summary>
        public float Spin { get; private set; }

        /// <summary>+1 spinning forward (earth thrown back), -1 spinning in reverse.</summary>
        public float Direction { get; private set; } = 1f;

        /// <summary>Shift held with the stick all the way forward or back.</summary>
        public bool FullGas { get; private set; }

        public TankWheelSpinBehaviour(TankEngine engine, float boostMultiplier)
        {
            _engine = engine;
            _boostMultiplier = Mathf.Max(1f, boostMultiplier);
        }

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();
            _body = entity.GetRiggedBody();
            _suspension = entity.GetSuspension();
            _input = entity.GetDriveInput();
            _boost = entity.TryGetDriveBoost(out IReactiveVariable<bool> boost) ? boost : null;
            _maxSpeed = entity.GetMoveSpeed();
            _driver = entity.GetDriver();

            _ruts = _hull.GetComponentInParent<VoxelTrackRuts>();
            _rammer = _hull.GetComponentInParent<VoxelRammer>();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            bool running = (_engine == null || _engine.Running) && _driver.Value != null;
            float throttle = running ? Mathf.Clamp(_input.Value.y, -1f, 1f) : 0f;
            bool boosting = _boost != null && _boost.Value && Mathf.Abs(throttle) > 0.1f;

            FullGas = boosting && Mathf.Abs(throttle) >= FULL_STICK;

            float target = 0f;
            if (boosting && _suspension.Grip > 0f)
            {
                Direction = Mathf.Sign(throttle);
                float top = Mathf.Max(0.1f, _maxSpeed.Value * _boostMultiplier);
                float along = Vector3.Dot(_body.linearVelocity, _hull.forward) * Direction;
                float share = Mathf.Clamp01(along / top);
                target = Mathf.Abs(throttle) * Mathf.Lerp(1f, SPIN_AT_TOP, share);
            }

            float rate = target > Spin ? RISE : FALL;
            Spin = Mathf.Lerp(Spin, target, 1f - Mathf.Exp(-rate * deltaTime));
            if (Spin < 0.001f)
                Spin = 0f;

            if (_ruts != null)
            {
                _ruts.WheelSpin = Spin;
                _ruts.SpinDirection = Direction;
            }

            if (_rammer != null)
            {
                _rammer.FullGas = FullGas;
                _rammer.Airborne = _suspension.Grip <= 0f;
            }
        }
    }
}
