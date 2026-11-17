using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The turret's traverse motor: tank.turret is how fast the turret turns against the hull, 0..1 of its top
    /// rate, and the loop runs while it turns.
    /// </summary>
    public sealed class TankTurretAudioBehaviour : IEntityInit, IEntityTick, IEntityDisable, IEntityDispose
    {
        private const float TURN_ON = 0.05f;
        private const float TURN_OFF = 0.02f;

        /// <summary>How fast (1/s) the parameter follows the turret: smooths the frame-to-frame steps.</summary>
        private const float RATE = 12f;

        private readonly AudioLoop _loop = new(AudioKeys.Tank.TURRET_LOOP, 0.15f);

        private Transform _hull;
        private Transform _pivot;
        private IValue<float> _maxRate;

        private float _lastYaw;
        private float _turn;

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();
            _pivot = entity.GetTurretPivot();
            _maxRate = entity.GetTurretRotationSpeed();
            _lastYaw = RelativeYaw();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            if (deltaTime <= 0f || _pivot == null)
                return;

            float yaw = RelativeYaw();
            float rate = Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw)) / deltaTime;
            _lastYaw = yaw;

            float share = Mathf.Clamp01(rate / Mathf.Max(1f, _maxRate.Value));
            _turn = Mathf.Lerp(_turn, share, 1f - Mathf.Exp(-RATE * deltaTime));
            if (_turn < 0.001f)
                _turn = 0f;

            GameAudio.SetParameter(AudioKeys.Tank.TURRET, _turn);
            _loop.Set(_turn > (_loop.IsPlaying ? TURN_OFF : TURN_ON), _hull);
        }

        public void Disable(IEntity entity)
        {
            _loop.Stop();
            _turn = 0f;
            GameAudio.SetParameter(AudioKeys.Tank.TURRET, 0f);
        }

        public void Dispose(IEntity entity) => _loop.StopNow();

        // Against the hull: a turret holding its aim while the hull turns under it is turning too.
        private float RelativeYaw() => _pivot.eulerAngles.y - _hull.eulerAngles.y;
    }
}
