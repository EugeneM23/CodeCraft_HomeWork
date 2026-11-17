using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The tank's engine: the starter as it cranks, the running loop, the stop when it dies; the boost kick when
    /// Shift goes down. Sets tank.rpm, tank.throttle and tank.speed (0..1) for the loops' curves.
    /// </summary>
    public sealed class TankEngineAudioBehaviour : IEntityInit, IEntityTick, IEntityDisable, IEntityDispose
    {
        private readonly TankEngine _engine;
        private readonly AudioLoop _loop = new(AudioKeys.Tank.ENGINE_LOOP, 0.4f);

        private Transform _hull;
        private Rigidbody _body;
        private IValue<Vector2> _input;
        private IValue<bool> _boost;
        private IValue<float> _maxSpeed;

        private TankEngine.EngineState _state;
        private bool _boosting;
        private float _rpm;

        public TankEngineAudioBehaviour(TankEngine engine) => _engine = engine;

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();
            entity.TryGetRiggedBody(out _body);
            if (entity.TryGetDriveInput(out IReactiveVariable<Vector2> input))
                _input = input;
            if (entity.TryGetDriveBoost(out IReactiveVariable<bool> boost))
                _boost = boost;
            if (entity.TryGetMoveSpeed(out IReactiveVariable<float> maxSpeed))
                _maxSpeed = maxSpeed;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            TankEngine.EngineState state = _engine.State;
            if (state != _state)
                OnStateChanged(_state, state);

            bool running = state == TankEngine.EngineState.Running;
            float throttle = running && _input != null ? _input.Value.y : 0f;
            bool boost = running && _boost != null && _boost.Value;
            float maxSpeed = _maxSpeed != null ? _maxSpeed.Value : 1f;

            Vector3 velocity = _body != null ? _body.linearVelocity : Vector3.zero;
            float forward = Vector3.Dot(velocity, _hull.forward);
            velocity.y = 0f;

            float target = state switch
            {
                TankEngine.EngineState.Running => TankAudioUseCase.TargetRpm(forward, maxSpeed, throttle, boost),
                TankEngine.EngineState.Starting => _engine.StartProgress * TankAudioUseCase.IDLE_RPM,
                _ => 0f
            };
            _rpm = TankAudioUseCase.SmoothRpm(_rpm, target, deltaTime);

            if (boost && !_boosting && Mathf.Abs(throttle) > 0.1f)
                GameAudio.Play(AudioKeys.Tank.BOOST, _hull);
            _boosting = boost;

            GameAudio.SetParameter(AudioKeys.Tank.RPM, _rpm);
            GameAudio.SetParameter(AudioKeys.Tank.THROTTLE, Mathf.Clamp01(Mathf.Abs(throttle)));
            GameAudio.SetParameter(AudioKeys.Tank.SPEED, TankAudioUseCase.SpeedShare(velocity.magnitude, maxSpeed));

            // Retried each frame while it runs: a pause or a missing bank may have refused the first start.
            if (running)
                _loop.Start(_hull);
        }

        public void Disable(IEntity entity)
        {
            _loop.Stop();
            _state = TankEngine.EngineState.Off;
            _boosting = false;
            _rpm = 0f;
            GameAudio.SetParameter(AudioKeys.Tank.RPM, 0f);
            GameAudio.SetParameter(AudioKeys.Tank.THROTTLE, 0f);
            GameAudio.SetParameter(AudioKeys.Tank.SPEED, 0f);
        }

        public void Dispose(IEntity entity) => _loop.StopNow();

        private void OnStateChanged(TankEngine.EngineState from, TankEngine.EngineState to)
        {
            _state = to;

            if (to == TankEngine.EngineState.Starting)
                GameAudio.Play(AudioKeys.Tank.ENGINE_START, _hull);

            if (to == TankEngine.EngineState.Off)
            {
                _loop.Stop();
                if (from == TankEngine.EngineState.Running)
                    GameAudio.Play(AudioKeys.Tank.ENGINE_STOP, _hull);
            }
        }
    }
}
