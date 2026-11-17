using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.AudioEngine.Adapters
{
    /// <summary>
    /// The running gear: the tracks rattling while the tank moves, squelching over mud, spinning flat out on
    /// Shift; the thump of landing after a jump. Sets tank.mud (share of the grounded wheels on a MudSurface)
    /// and tank.slip (TankWheelSpinBehaviour.Spin).
    /// </summary>
    public sealed class TankTracksAudioBehaviour : IEntityInit, IEntityTick, IEntityDisable, IEntityDispose
    {
        /// <summary>Speed share above which the tracks sound, and below which they stop: a gap against flicker.</summary>
        private const float MOVE_ON = 0.05f;
        private const float MOVE_OFF = 0.02f;

        private const float MUD_ON = 0.1f;
        private const float MUD_OFF = 0.05f;
        private const float SLIP_ON = 0.05f;
        private const float SLIP_OFF = 0.02f;

        /// <summary>Seconds with no wheel on the ground before touching down counts as a landing.</summary>
        private const float MIN_AIR_TIME = 0.3f;

        /// <summary>Metres from the hull's centre to a track: turning on the spot moves the tracks this fast per rad/s.</summary>
        private const float TRACK_ARM = 1.2f;

        /// <summary>How fast (1/s) the mud share follows the wheels.</summary>
        private const float MUD_RATE = 4f;

        private readonly TankWheelSpinBehaviour _wheelSpin;
        private readonly AudioLoop _tracks = new(AudioKeys.Tank.TRACKS_LOOP, 0.3f);
        private readonly AudioLoop _mudTracks = new(AudioKeys.Tank.TRACKS_MUD_LOOP, 0.4f);
        private readonly AudioLoop _spin = new(AudioKeys.Tank.WHEEL_SPIN_LOOP, 0.3f);

        private Transform _hull;
        private Rigidbody _body;
        private TankSuspension _suspension;
        private IValue<float> _maxSpeed;

        // What each wheel stood on when last looked at, and whether that was mud: GetComponent only on a change.
        private Collider[] _grounds;
        private bool[] _onMud;

        private float _mud;
        private float _airTime;

        public TankTracksAudioBehaviour(TankWheelSpinBehaviour wheelSpin) => _wheelSpin = wheelSpin;

        public void Init(IEntity entity)
        {
            _hull = entity.GetTransform();
            entity.TryGetRiggedBody(out _body);
            _suspension = entity.GetSuspension();
            if (entity.TryGetMoveSpeed(out IReactiveVariable<float> maxSpeed))
                _maxSpeed = maxSpeed;

            int wheels = _suspension.Wheels.Length;
            _grounds = new Collider[wheels];
            _onMud = new bool[wheels];
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            bool grounded = _suspension.GroundedCount > 0 || _suspension.Resting;

            float speed = 0f;
            if (_body != null)
            {
                Vector3 velocity = _body.linearVelocity;
                velocity.y = 0f;
                float trackSpeed = velocity.magnitude + Mathf.Abs(_body.angularVelocity.y) * TRACK_ARM;
                speed = TankAudioUseCase.SpeedShare(trackSpeed, _maxSpeed != null ? _maxSpeed.Value : 1f);
            }

            _mud = Mathf.Lerp(_mud, MudShare(), 1f - Mathf.Exp(-MUD_RATE * deltaTime));
            float slip = _wheelSpin != null ? _wheelSpin.Spin : 0f;

            GameAudio.SetParameter(AudioKeys.Tank.MUD, _mud);
            GameAudio.SetParameter(AudioKeys.Tank.SLIP, slip);

            bool moving = grounded && speed > (_tracks.IsPlaying ? MOVE_OFF : MOVE_ON);
            _tracks.Set(moving, _hull);
            _mudTracks.Set(moving && _mud > (_mudTracks.IsPlaying ? MUD_OFF : MUD_ON), _hull);
            _spin.Set(slip > (_spin.IsPlaying ? SLIP_OFF : SLIP_ON), _hull);

            if (!grounded)
            {
                _airTime += deltaTime;
            }
            else
            {
                if (_airTime >= MIN_AIR_TIME)
                    GameAudio.Play(AudioKeys.Tank.LANDING, _hull);
                _airTime = 0f;
            }
        }

        public void Disable(IEntity entity)
        {
            _tracks.Stop();
            _mudTracks.Stop();
            _spin.Stop();
            _airTime = 0f;
        }

        public void Dispose(IEntity entity)
        {
            _tracks.StopNow();
            _mudTracks.StopNow();
            _spin.StopNow();
        }

        private float MudShare()
        {
            TankWheel[] wheels = _suspension.Wheels;
            int grounded = 0;
            int onMud = 0;

            for (int i = 0; i < wheels.Length; i++)
            {
                TankWheel wheel = wheels[i];
                if (!wheel.Grounded || wheel.Ground == null)
                    continue;

                grounded++;
                if (wheel.Ground != _grounds[i])
                {
                    _grounds[i] = wheel.Ground;
                    _onMud[i] = wheel.Ground.TryGetComponent(out MudSurface _);
                }

                if (_onMud[i])
                    onMud++;
            }

            return grounded == 0 ? 0f : (float)onMud / grounded;
        }
    }
}
