using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The air strike skill (GDD §3): while <see cref="CombatModifiers.AirStrikeLevel"/> is above 0, every
    /// 10/9/8/7/6 s (WeaponSettings) a bomb falls on the densest crowd within 30 m of the tank
    /// (<see cref="EnemyTargets.TryFindDensest"/>) and deals 400 to every enemy target within 4 m where it lands
    /// (entities included: the prototype's bomb damages none). From level 5 a second bomb falls on the nearest enemy
    /// outside the first one's blast. The bombs are the sandbox's <see cref="VoxelAirStrike"/> (crater, fireballs,
    /// shake), whose own random timer is held while the skill is on; a level without it gets the damage and an
    /// <see cref="ExplosionEvents"/> blast without the bomb. On the tank; added by <see cref="TargetingSetup"/>.
    /// </summary>
    public sealed class AirStrikeSkillBehaviour : IEntityInit, IEntityTick, IEntityDispose
    {
        private readonly EnemyTargets _targets;
        private readonly WeaponSettings _settings;
        private readonly VoxelAirStrike _strike;

        private CombatModifiers _modifiers;
        private Transform _tank;
        private bool _suppressing;
        private float _next = -1f;

        /// <param name="strike">The level's air strike prototype, found once at attach; null: damage only.</param>
        public AirStrikeSkillBehaviour(EnemyTargets targets, WeaponSettings settings, VoxelAirStrike strike)
        {
            _targets = targets;
            _settings = settings;
            _strike = strike;
        }

        public void Init(IEntity tank)
        {
            tank.TryGetCombatModifiers(out _modifiers);
            tank.TryGetTransform(out _tank);
        }

        public void Dispose(IEntity tank) => Suppress(false);

        public void Tick(IEntity tank, float deltaTime)
        {
            int level = _modifiers != null ? _modifiers.AirStrikeLevel : 0;
            if (level <= 0 || _tank == null || _targets == null)
            {
                Suppress(false);
                _next = -1f;
                return;
            }

            Suppress(true);

            float now = GameTicks.Time;
            float interval = _settings.GetAirStrikeInterval(level);
            if (_next < 0f)
                _next = now + interval; // the first bomb a whole interval after the skill is taken
            if (now < _next)
                return;

            _next = now + interval;
            Vector3 from = _tank.position;
            if (!_targets.TryFindDensest(from, _settings.airStrikeSearchRange, _settings.airStrikeRadius,
                    out Vector3 first))
                return;

            Drop(first);

            if (level >= _settings.airStrikeTwoBombsLevel &&
                _targets.TryFindNearestBeyond(first, _settings.airStrikeRadius * 1.5f, _settings.airStrikeSearchRange,
                    out _, out Vector3 second))
                Drop(second);
        }

        private void Drop(Vector3 point)
        {
            if (_strike != null)
            {
                _strike.Strike(point, _settings.airStrikeDamage, _settings.airStrikeRadius, _settings.airStrikeMinFactor);
                return;
            }

            _targets.DamageArea(point, _settings.airStrikeRadius, _settings.airStrikeDamage, _settings.airStrikeMinFactor);
            ExplosionEvents.Raise(point, _settings.airStrikeRadius);
        }

        private void Suppress(bool on)
        {
            if (_suppressing == on)
                return;
            _suppressing = on;
            if (_strike != null)
                _strike.TimerSuppressed = on;
        }
    }
}
