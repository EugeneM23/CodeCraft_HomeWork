using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Black diesel smoke out of the exhaust pipes at the back of the hull.
    ///
    /// Starting (TankEngine): the jets grow from a cough to thick plumes over the two seconds the
    /// engine cranks, flickering as it catches. Running, it is the load that counts, not the speed:
    /// picking up speed the engine labours and the pipes pour smoke, the lowest gears most; rolling
    /// at a steady speed only a thin stream comes out, idling less still. The speed range is split
    /// into <see cref="GEARS"/> gears: in each the smoke eases off as it revs up, and on every shift
    /// up it surges again for a moment.
    ///
    /// The plume is many small, faint puffs that overlap and swell into one another; a few big dark
    /// ones read as separate balls, the texture having a dense round core. On top of the rate the
    /// pipes emit by distance, so the trail has no gaps at speed. It simulates in world space and
    /// is left behind; with the engine off the pipes stop emitting and what is out thins away.
    /// </summary>
    public sealed class TankExhaustBehaviour : IEntityInit, IEntityTick
    {
        private const int GEARS = 4;

        // Idle → full load while running.
        private const float IDLE_RATE = 14f;
        private const float FULL_RATE = 300f;
        private const float IDLE_SPEED = 0.6f;
        private const float FULL_SPEED = 5f;
        private const float IDLE_SIZE = 0.1f;
        private const float FULL_SIZE = 0.2f;
        private const float IDLE_ALPHA = 0.22f;
        private const float FULL_ALPHA = 0.45f;

        /// <summary>Shift held on the gas: this many times the smoke, reached within <see cref="RISE_TIME"/>.</summary>
        private const float BOOST_SMOKE = 4f;

        /// <summary>Puffs a metre the hull travels, on top of the rate.</summary>
        private const float RATE_PER_METRE = 16f;

        // Cranking: from the first cough to the moment it catches.
        private const float CRANK_RATE_FROM = 25f;
        private const float CRANK_RATE_TO = 220f;
        private const float CRANK_SPEED_FROM = 1.2f;
        private const float CRANK_SPEED_TO = 6f;
        private const float CRANK_SIZE_FROM = 0.12f;
        private const float CRANK_SIZE_TO = 0.26f;
        private const float CRANK_ALPHA_FROM = 0.3f;
        private const float CRANK_ALPHA_TO = 0.55f;

        /// <summary>Load rolling at a steady speed: thin stream, a touch more the faster.</summary>
        private const float CRUISE_LOAD = 0.3f;
        private const float CRUISE_LOAD_FAST = 0.3f;

        /// <summary>How much the smoke eases off from the bottom to the top of a gear.</summary>
        private const float GEAR_EASE = 0.45f;

        /// <summary>How much less the top gear smokes than the first.</summary>
        private const float HIGH_GEAR_EASE = 0.5f;

        /// <summary>Load a shift up adds for a moment, fading over <see cref="SURGE_TIME"/> seconds.</summary>
        private const float SHIFT_SURGE = 0.5f;
        private const float SURGE_TIME = 0.35f;

        /// <summary>Time constants (s): acceleration smoothing, smoke rising, smoke falling.</summary>
        private const float ACCEL_TIME = 0.2f;
        private const float RISE_TIME = 0.12f;
        private const float FALL_TIME = 0.5f;

        private static readonly Color SmokeDark = new(0.07f, 0.07f, 0.07f);
        private static readonly Color SmokeLight = new(0.14f, 0.135f, 0.13f);

        private readonly ParticleSystem[] _pipes;
        private readonly TankEngine _engine;
        private readonly float _acceleration;
        private readonly float _boostMultiplier;

        private Rigidbody _body;
        private IValue<float> _topSpeed;
        private IValue<Vector2> _input;
        private IValue<bool> _boost;

        private float _boostSmoke = 1f;
        private float _lastSpeed;
        private float _accel;
        private int _gear;
        private float _surge;

        private float _rate;
        private float _speed = IDLE_SPEED;
        private float _size = IDLE_SIZE;
        private float _alpha = IDLE_ALPHA;

        public TankExhaustBehaviour(ParticleSystem[] pipes, TankEngine engine, float acceleration, float boostMultiplier)
        {
            _pipes = pipes ?? System.Array.Empty<ParticleSystem>();
            _engine = engine;
            _acceleration = Mathf.Max(0.1f, acceleration);
            _boostMultiplier = Mathf.Max(1f, boostMultiplier);
        }

        public void Init(IEntity entity)
        {
            _body = entity.GetRiggedBody();
            _topSpeed = entity.GetMoveSpeed();
            _input = entity.GetDriveInput();
            _boost = entity.TryGetDriveBoost(out IReactiveVariable<bool> boost) ? boost : null;

            foreach (ParticleSystem pipe in _pipes)
            {
                if (pipe == null)
                    continue;

                var emission = pipe.emission;
                emission.rateOverTime = 0f;
                emission.rateOverDistance = 0f;
                pipe.Play(true);
            }
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            Vector3 velocity = _body.linearVelocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            float measured = (speed - _lastSpeed) / deltaTime;
            _lastSpeed = speed;
            _accel = Mathf.Lerp(_accel, measured, 1f - Mathf.Exp(-deltaTime / ACCEL_TIME));
            _surge = Mathf.MoveTowards(_surge, 0f, SHIFT_SURGE / SURGE_TIME * deltaTime);

            float rate, jet, size, alpha;
            switch (_engine.State)
            {
                case TankEngine.EngineState.Starting:
                {
                    float p = _engine.StartProgress;
                    // Grows faster towards the end; flickers as the cylinders catch one by one.
                    float grow = p * p * (3f - 2f * p);
                    float flicker = 0.7f + 0.3f * Mathf.PerlinNoise(Time.time * 9f, 0.37f);
                    rate = Mathf.Lerp(CRANK_RATE_FROM, CRANK_RATE_TO, grow) * flicker;
                    jet = Mathf.Lerp(CRANK_SPEED_FROM, CRANK_SPEED_TO, grow) * flicker;
                    size = Mathf.Lerp(CRANK_SIZE_FROM, CRANK_SIZE_TO, grow);
                    alpha = Mathf.Lerp(CRANK_ALPHA_FROM, CRANK_ALPHA_TO, grow);
                    _gear = 0;
                    break;
                }

                case TankEngine.EngineState.Running:
                {
                    float load = Load(speed);
                    rate = Mathf.Lerp(IDLE_RATE, FULL_RATE, load);
                    jet = Mathf.Lerp(IDLE_SPEED, FULL_SPEED, load);
                    size = Mathf.Lerp(IDLE_SIZE, FULL_SIZE, load);
                    alpha = Mathf.Lerp(IDLE_ALPHA, FULL_ALPHA, load);
                    break;
                }

                default:
                    rate = 0f;
                    jet = _speed;
                    size = _size;
                    alpha = _alpha;
                    _gear = 0;
                    break;
            }

            // Shift on the gas: the engine at full chat, BOOST_SMOKE times the smoke.
            bool boosting = _engine.Running && _boost != null && _boost.Value && _input.Value.sqrMagnitude > 0.01f;
            _boostSmoke = Smooth(_boostSmoke, boosting ? BOOST_SMOKE : 1f, deltaTime);
            rate *= _boostSmoke;

            _rate = rate == 0f ? 0f : Smooth(_rate, rate, deltaTime);
            _speed = Smooth(_speed, jet, deltaTime);
            _size = Smooth(_size, size, deltaTime);
            _alpha = Smooth(_alpha, alpha, deltaTime);

            Apply(_engine.State != TankEngine.EngineState.Off);
        }

        /// <summary>
        /// 0 idling, 1 the engine labouring in first gear. Gaining speed is what loads it; a steady
        /// roll only a little.
        /// </summary>
        private float Load(float speed)
        {
            float top = Mathf.Max(0.1f, _topSpeed.Value * _boostMultiplier);
            float speed01 = Mathf.Clamp01(speed / top);
            float throttle = Mathf.Clamp01(_input.Value.magnitude);

            float gearPosition = speed01 * GEARS;
            int gear = Mathf.Min(GEARS - 1, Mathf.FloorToInt(gearPosition));
            float rev = gearPosition - gear;

            // Gaining speed against what full throttle (boosted) gains: 1 flat out.
            float gaining = Mathf.Clamp01(_accel / (_acceleration * _boostMultiplier * 0.6f)) * throttle;

            // A shift up: the next gear takes the load and the smoke surges for a moment.
            if (gear > _gear && gaining > 0.2f)
                _surge = SHIFT_SURGE;
            _gear = gear;

            float lowGear = 1f - HIGH_GEAR_EASE * gear / (GEARS - 1f);
            float inGear = 1f - GEAR_EASE * rev;
            float pull = gaining * lowGear * inGear;

            float cruise = throttle * (CRUISE_LOAD + CRUISE_LOAD_FAST * speed01);
            return Mathf.Clamp01(Mathf.Max(pull, cruise) + _surge);
        }

        private void Apply(bool running)
        {
            Color dark = SmokeDark;
            dark.a = _alpha;
            Color light = SmokeLight;
            light.a = _alpha * 0.7f;

            foreach (ParticleSystem pipe in _pipes)
            {
                if (pipe == null)
                    continue;

                var emission = pipe.emission;
                emission.rateOverTime = _rate;
                emission.rateOverDistance = running ? RATE_PER_METRE * _boostSmoke : 0f;

                var main = pipe.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(_speed * 0.7f, _speed * 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(_size * 0.7f, _size * 1.3f);
                main.startColor = new ParticleSystem.MinMaxGradient(dark, light);
            }
        }

        private static float Smooth(float current, float target, float deltaTime)
        {
            float time = target > current ? RISE_TIME : FALL_TIME;
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-deltaTime / time));
        }
    }
}
