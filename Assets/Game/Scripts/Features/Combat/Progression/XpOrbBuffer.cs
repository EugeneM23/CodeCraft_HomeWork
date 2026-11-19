using UnityEngine;

namespace Game
{
    /// <summary>
    /// The experience orbs as plain arrays of fixed capacity: where each is, how it moves, what it is worth. An orb
    /// hops out of a dead enemy and falls to the ground height found when it dropped, lies there, and flies to the
    /// tank — accelerating — once the tank is within the pickup radius or everything is pulled (ArenaLeft). A full
    /// buffer merges a new drop into the nearest orb on the ground, so no experience is lost. Collected orbs are
    /// listed for the step that collected them (<see cref="CollectedCount"/>). No allocation after construction.
    /// </summary>
    public sealed class XpOrbBuffer
    {
        public enum OrbState : byte
        {
            Falling,
            Lying,
            Flying
        }

        private readonly Vector3[] _position;
        private readonly Vector3[] _velocity;
        private readonly float[] _groundY;
        private readonly float[] _speed;
        private readonly float[] _phase;
        private readonly int[] _value;
        private readonly OrbState[] _state;

        private readonly Vector3[] _collectedPoint;
        private readonly int[] _collectedValue;

        private readonly float _gravity;
        private readonly float _collectDistance;
        private readonly float _flySpeedMin;
        private readonly float _flySpeedMax;
        private readonly float _flyAcceleration;

        public XpOrbBuffer(int capacity, float gravity = 20f, float collectDistance = 0.6f, float flySpeedMin = 6f,
            float flySpeedMax = 25f, float flyAcceleration = 40f)
        {
            Capacity = Mathf.Max(1, capacity);
            _position = new Vector3[Capacity];
            _velocity = new Vector3[Capacity];
            _groundY = new float[Capacity];
            _speed = new float[Capacity];
            _phase = new float[Capacity];
            _value = new int[Capacity];
            _state = new OrbState[Capacity];
            _collectedPoint = new Vector3[Capacity];
            _collectedValue = new int[Capacity];

            _gravity = gravity;
            _collectDistance = Mathf.Max(0.01f, collectDistance);
            _flySpeedMin = Mathf.Max(0.1f, flySpeedMin);
            _flySpeedMax = Mathf.Max(_flySpeedMin, flySpeedMax);
            _flyAcceleration = Mathf.Max(0f, flyAcceleration);
        }

        public int Capacity { get; }

        public int Count { get; private set; }

        /// <summary>Orbs collected by the last <see cref="Step"/>.</summary>
        public int CollectedCount { get; private set; }

        public Vector3 GetPosition(int index) => _position[index];

        public int GetValue(int index) => _value[index];

        public OrbState GetState(int index) => _state[index];

        public float GetPhase(int index) => _phase[index];

        public void GetCollected(int index, out Vector3 point, out int value)
        {
            point = _collectedPoint[index];
            value = _collectedValue[index];
        }

        /// <summary>Total experience lying, falling and flying.</summary>
        public int TotalValue()
        {
            int total = 0;
            for (int i = 0; i < Count; i++)
                total += _value[i];
            return total;
        }

        /// <summary>
        /// A new orb at <paramref name="position"/> moving with <paramref name="velocity"/>, landing at
        /// <paramref name="groundY"/>. Full: the value goes to the nearest orb (lying first). Returns the orb's index.
        /// </summary>
        public int Spawn(Vector3 position, Vector3 velocity, float groundY, int value, float phase = 0f)
        {
            if (value <= 0)
                return -1;

            if (Count >= Capacity)
            {
                int nearest = Nearest(position);
                _value[nearest] += value;
                return nearest;
            }

            int i = Count++;
            _position[i] = position;
            _velocity[i] = velocity;
            _groundY[i] = groundY;
            _speed[i] = 0f;
            _phase[i] = phase;
            _value[i] = value;
            _state[i] = position.y <= groundY && velocity.y <= 0f ? OrbState.Lying : OrbState.Falling;
            if (_state[i] == OrbState.Lying)
                _position[i].y = groundY;
            return i;
        }

        /// <summary>Every orb flies to the tank (the tank is through the gate).</summary>
        public void PullAll()
        {
            for (int i = 0; i < Count; i++)
                StartFlying(i);
        }

        public void Clear()
        {
            Count = 0;
            CollectedCount = 0;
        }

        /// <summary>
        /// Moves the orbs by <paramref name="deltaTime"/>. With a tank (<paramref name="hasTarget"/>) the orbs within
        /// <paramref name="pickupRadius"/> of <paramref name="target"/> start flying, and flying orbs that reach it are
        /// collected and removed. Without one flying orbs hang where they are.
        /// </summary>
        public void Step(float deltaTime, Vector3 target, bool hasTarget, float pickupRadius)
        {
            CollectedCount = 0;
            if (deltaTime <= 0f)
                return;

            float pickupSqr = pickupRadius * pickupRadius;

            for (int i = Count - 1; i >= 0; i--)
            {
                switch (_state[i])
                {
                    case OrbState.Falling:
                    {
                        Vector3 v = _velocity[i];
                        v.y -= _gravity * deltaTime;
                        Vector3 p = _position[i] + v * deltaTime;
                        if (p.y <= _groundY[i] && v.y <= 0f)
                        {
                            p.y = _groundY[i];
                            v = Vector3.zero;
                            _state[i] = OrbState.Lying;
                        }

                        _position[i] = p;
                        _velocity[i] = v;
                        if (hasTarget && (target - p).sqrMagnitude <= pickupSqr)
                            StartFlying(i);
                        break;
                    }
                    case OrbState.Lying:
                        if (hasTarget && (target - _position[i]).sqrMagnitude <= pickupSqr)
                            StartFlying(i);
                        break;
                }

                if (_state[i] != OrbState.Flying || !hasTarget)
                    continue;

                _speed[i] = Mathf.Min(_speed[i] + _flyAcceleration * deltaTime, _flySpeedMax);

                Vector3 toTarget = target - _position[i];
                float distance = toTarget.magnitude;
                float travel = _speed[i] * deltaTime;
                if (distance <= _collectDistance || travel >= distance - _collectDistance)
                {
                    Collect(i);
                    continue;
                }

                _position[i] += toTarget * (travel / distance);
            }
        }

        private void StartFlying(int i)
        {
            if (_state[i] == OrbState.Flying)
                return;

            _state[i] = OrbState.Flying;
            _speed[i] = _flySpeedMin;
            _velocity[i] = Vector3.zero;
        }

        private void Collect(int i)
        {
            _collectedPoint[CollectedCount] = _position[i];
            _collectedValue[CollectedCount] = _value[i];
            CollectedCount++;
            RemoveAt(i);
        }

        // Swap with the last: Step walks backwards, so the orb moved into i has already been stepped
        private void RemoveAt(int i)
        {
            int last = --Count;
            if (i == last)
                return;

            _position[i] = _position[last];
            _velocity[i] = _velocity[last];
            _groundY[i] = _groundY[last];
            _speed[i] = _speed[last];
            _phase[i] = _phase[last];
            _value[i] = _value[last];
            _state[i] = _state[last];
        }

        /// <summary>The nearest lying orb; with none lying the nearest falling one; else the nearest of all.</summary>
        private int Nearest(Vector3 position)
        {
            int best = -1;
            int bestRank = int.MaxValue;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < Count; i++)
            {
                int rank = _state[i] == OrbState.Lying ? 0 : _state[i] == OrbState.Falling ? 1 : 2;
                float sqr = (_position[i] - position).sqrMagnitude;
                if (rank < bestRank || (rank == bestRank && sqr < bestSqr))
                {
                    best = i;
                    bestRank = rank;
                    bestSqr = sqr;
                }
            }

            return best;
        }
    }
}
