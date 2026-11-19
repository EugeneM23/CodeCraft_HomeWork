using UnityEngine;

namespace Game
{
    /// <summary>
    /// What an auto gun aims at: the cursor point while the player holds focus (<see cref="CombatFocus"/>), otherwise
    /// the nearest live enemy within range, searched again every <c>retargetInterval</c> and followed every tick in
    /// between. Shared by the machine gun and the turrets. No allocations.
    /// </summary>
    public sealed class AutoTargetTracker
    {
        private float _range;
        private readonly float _interval;

        private IEnemyTargets _targets;
        private EnemyRef _target = EnemyRef.None;
        private float _searchIn;

        public AutoTargetTracker(float range, float interval)
        {
            _range = range;
            _interval = Mathf.Max(0.02f, interval);
        }

        public void Bind(IEnemyTargets targets)
        {
            _targets = targets;
            _target = EnemyRef.None;
            _searchIn = 0f;
        }

        /// <summary>Search range (m); the machine gun follows its WeaponRange through it.</summary>
        public float Range
        {
            get => _range;
            set => _range = value;
        }

        /// <summary>The point to aim at this tick, measured from <paramref name="muzzle"/>. False: nothing to shoot.</summary>
        public bool TryGetAimPoint(Vector3 muzzle, float deltaTime, out Vector3 point)
        {
            if (CombatFocus.Active)
            {
                point = CombatFocus.Point;
                return true;
            }

            point = default;
            if (_targets == null)
                return false;

            _searchIn -= deltaTime;
            if (_searchIn <= 0f)
                return Search(muzzle, out point);

            if (!_target.IsValid)
                return false; // nothing found last time: wait for the next search

            if (_targets.IsAlive(_target))
            {
                point = _targets.GetPosition(_target);
                if ((point - muzzle).sqrMagnitude <= _range * _range)
                    return true;
            }

            // Lost between searches (dead or out of range): the next search at once.
            return Search(muzzle, out point);
        }

        private bool Search(Vector3 muzzle, out Vector3 point)
        {
            _searchIn = _interval;
            if (_targets.TryFindNearest(muzzle, _range, out _target, out point))
                return true;
            _target = EnemyRef.None;
            return false;
        }
    }
}
