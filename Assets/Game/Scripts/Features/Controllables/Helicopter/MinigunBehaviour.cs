using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The helicopter's rotary gun: a cluster of barrels that spins about the gun's forward. The
    /// trigger (<see cref="Pull"/>, called by the helicopter's FireAction every frame the button is
    /// held) spins it up; let go, it winds down. Once it turns fast enough, a round leaves every
    /// time the next barrel comes round — every 360 / barrels degrees — so the rate climbs with the
    /// spin and tops out at maxSpin / (360 / barrels) rounds a second, several in one frame if need be.
    ///
    /// A round fires every gun's FireAction straight away: the guns' own cooldown is not looked at,
    /// the barrels set the rate.
    /// </summary>
    public sealed class MinigunBehaviour : IEntityTick
    {
        // A frame hitch must not turn into one burst of a hundred bullets.
        private const int MAX_ROUNDS_PER_TICK = 8;

        private readonly Transform _barrels;
        private readonly IEntity[] _guns;
        private readonly float _maxSpin;
        private readonly float _stepAngle;
        private readonly float _spinUpTime;
        private readonly float _spinDownTime;
        private readonly float _fireThrottle;

        private bool _pulled;
        private bool _firing;
        private float _throttle;
        private float _sinceRound;

        /// <param name="barrels">Spun about its local forward; may be null (no visual).</param>
        /// <param name="maxSpin">Degrees per second at full speed.</param>
        /// <param name="fireThrottle">Share (0..1] of full speed below which it spins without firing.</param>
        public MinigunBehaviour(Transform barrels, IEntity[] guns, int barrelCount, float maxSpin, float spinUpTime,
            float spinDownTime, float fireThrottle)
        {
            _barrels = barrels;
            _guns = guns ?? new IEntity[0];
            _stepAngle = 360f / Mathf.Max(1, barrelCount);
            _maxSpin = maxSpin;
            _spinUpTime = Mathf.Max(0.01f, spinUpTime);
            _spinDownTime = Mathf.Max(0.01f, spinDownTime);
            _fireThrottle = Mathf.Clamp(fireThrottle, 0.01f, 1f);
        }

        /// <summary>Share of full speed the barrels turn at now, 0..1.</summary>
        public float Throttle => _throttle;

        /// <summary>The trigger is held this frame. Read, and forgotten, by the next tick.</summary>
        public void Pull() => _pulled = true;

        public void Tick(IEntity entity, float deltaTime)
        {
            bool held = _pulled;
            _pulled = false;

            _throttle = held
                ? Mathf.MoveTowards(_throttle, 1f, deltaTime / _spinUpTime)
                : Mathf.MoveTowards(_throttle, 0f, deltaTime / _spinDownTime);

            float turn = _maxSpin * _throttle * deltaTime;

            if (_barrels != null && turn > 0f)
                _barrels.localRotation *= Quaternion.Euler(0f, 0f, turn);

            if (!held || _throttle < _fireThrottle)
            {
                _firing = false;
                return;
            }

            // The first round goes the moment the barrels are fast enough, not a barrel later.
            if (!_firing)
            {
                _firing = true;
                _sinceRound = _stepAngle;
            }
            else
            {
                _sinceRound += turn;
            }

            int rounds = 0;
            while (_sinceRound >= _stepAngle && rounds < MAX_ROUNDS_PER_TICK)
            {
                _sinceRound -= _stepAngle;
                rounds++;
                FireRound();
            }

            // What a hitch left over beyond the cap is dropped, not fired next frame.
            if (_sinceRound >= _stepAngle)
                _sinceRound %= _stepAngle;
        }

        private void FireRound()
        {
            foreach (IEntity gun in _guns)
                if (gun != null && gun.TryGetFireAction(out var fire))
                    fire.Invoke();
        }
    }
}
