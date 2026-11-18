using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The smoke screens in the level as plain circles on the ground (centre, radius, until when): what the AI asks
    /// to know whether the tank is hidden. Height is ignored; nothing reads the voxel smoke itself. GameContext key:
    /// SmokeScreen. Up to <see cref="Capacity"/> at once; a new one past that replaces the one ending soonest.
    /// Allocates nothing after construction.
    /// </summary>
    public sealed class SmokeScreen : ISmokeScreen
    {
        public const int DEFAULT_CAPACITY = 8;

        private static readonly Func<float> GameClock = () => GameTicks.Time;

        private readonly Vector2[] _centres;
        private readonly float[] _radii;
        private readonly float[] _until;
        private readonly Func<float> _clock;

        /// <param name="capacity">Screens kept at once.</param>
        /// <param name="clock">Game seconds; GameTicks.Time without one.</param>
        public SmokeScreen(int capacity = DEFAULT_CAPACITY, Func<float> clock = null)
        {
            capacity = Mathf.Max(1, capacity);
            _centres = new Vector2[capacity];
            _radii = new float[capacity];
            _until = new float[capacity];
            for (int i = 0; i < capacity; i++)
                _until[i] = float.NegativeInfinity;
            _clock = clock ?? GameClock;
        }

        public int Capacity => _until.Length;

        public bool IsActive
        {
            get
            {
                float now = _clock();
                for (int i = 0; i < _until.Length; i++)
                {
                    if (now < _until[i])
                        return true;
                }

                return false;
            }
        }

        /// <summary>A screen of this radius round the point for this many seconds from now.</summary>
        public void Add(Vector3 center, float radius, float seconds)
        {
            if (radius <= 0f || seconds <= 0f)
                return;

            float now = _clock();
            int slot = 0;
            float soonest = float.PositiveInfinity;
            for (int i = 0; i < _until.Length; i++)
            {
                if (_until[i] <= now)
                {
                    slot = i;
                    break;
                }

                if (_until[i] < soonest)
                {
                    soonest = _until[i];
                    slot = i;
                }
            }

            _centres[slot] = new Vector2(center.x, center.z);
            _radii[slot] = radius;
            _until[slot] = now + seconds;
        }

        /// <summary>Every screen gone at once (a new level, the tank gone).</summary>
        public void Clear()
        {
            for (int i = 0; i < _until.Length; i++)
                _until[i] = float.NegativeInfinity;
        }

        public bool Contains(Vector3 point)
        {
            float now = _clock();
            Vector2 p = new(point.x, point.z);
            for (int i = 0; i < _until.Length; i++)
            {
                if (now >= _until[i])
                    continue;
                if ((p - _centres[i]).sqrMagnitude <= _radii[i] * _radii[i])
                    return true;
            }

            return false;
        }

        public bool BlocksLine(Vector3 from, Vector3 to)
        {
            float now = _clock();
            Vector2 a = new(from.x, from.z);
            Vector2 b = new(to.x, to.z);
            for (int i = 0; i < _until.Length; i++)
            {
                if (now >= _until[i])
                    continue;
                if (SegmentTouchesCircle(a, b, _centres[i], _radii[i]))
                    return true;
            }

            return false;
        }

        /// <summary>The segment a–b comes within <paramref name="radius"/> of the centre (an end inside counts).</summary>
        public static bool SegmentTouchesCircle(Vector2 a, Vector2 b, Vector2 centre, float radius)
        {
            Vector2 ab = b - a;
            float lengthSqr = ab.sqrMagnitude;
            float t = lengthSqr > 1e-8f ? Mathf.Clamp01(Vector2.Dot(centre - a, ab) / lengthSqr) : 0f;
            Vector2 closest = a + ab * t;
            return (centre - closest).sqrMagnitude <= radius * radius;
        }
    }
}
