using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saving.Tests
{
    /// <summary>
    /// A script that keeps its state to itself, as game scripts do: a look angle, a flag, a phase, an aim, a moment of
    /// Time.time, a count of seconds — and next to them what is not state: a cached reference, a cache, scratch data,
    /// a constant.
    /// </summary>
    public sealed class PrivateStateProbe : MonoBehaviour
    {
        public enum Phase
        {
            Idle,
            Attack
        }

        public int Shown = 1;

        private float _angle;
        private bool _armed;
        private Phase _phase;
        private Vector3 _aim;

        private float _lastTimeFired = float.NegativeInfinity; // a moment of Time.time
        private float _elapsedTime;                             // a count of seconds
        private Vector3 _lastPosition;                          // where it was: state
#pragma warning disable CS0414 // what the script saw last time: only compared with the value of now
        private float _lastSeenRatio = 1f;
        private bool _wasArmed;
#pragma warning restore CS0414
        private Transform _cached;
        private List<int> _cache = new();
        [NonSerialized] private int _scratch;
        private readonly int _fixed = 3;

        public float Angle
        {
            get => _angle;
            set => _angle = value;
        }

        public bool Armed
        {
            get => _armed;
            set => _armed = value;
        }

        public Phase Now
        {
            get => _phase;
            set => _phase = value;
        }

        public Vector3 Aim
        {
            get => _aim;
            set => _aim = value;
        }

        public float LastTimeFired
        {
            get => _lastTimeFired;
            set => _lastTimeFired = value;
        }

        public float ElapsedTime
        {
            get => _elapsedTime;
            set => _elapsedTime = value;
        }

        public Transform Cached
        {
            get => _cached;
            set => _cached = value;
        }

        public Vector3 LastPosition
        {
            get => _lastPosition;
            set => _lastPosition = value;
        }

        /// <summary>The script's own "did it change" check, as a game writes it in Update.</summary>
        public bool SeesAChange(float ratio)
        {
            bool changed = ratio != _lastSeenRatio || _armed != _wasArmed;
            _lastSeenRatio = ratio;
            _wasArmed = _armed;
            return changed;
        }

        public int NotState => _cache.Count + _scratch + _fixed;
    }
}
