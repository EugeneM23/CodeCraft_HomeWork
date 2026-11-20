using System.Collections;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The special module on E: an air strike of <see cref="BOMBS"/> bombs, one every <see cref="INTERVAL"/> s, on the
    /// point the player aims at (the pointer on the ground, a house, a monster), scattered a little round it. Bombs and
    /// their damage are the level's <see cref="VoxelAirStrike"/> and the air strike's settings (WeaponSettings). It
    /// recharges over the special module's cooldown.
    /// </summary>
    public sealed class AirStrikeBarrageModule : ISpecialModule
    {
        public const int BOMBS = 10;
        public const float INTERVAL = 0.2f;

        /// <summary>How far (m) round the aim point the bombs land.</summary>
        private const float SCATTER = 3f;

        public delegate bool AimProvider(out Vector3 point);

        private readonly AimProvider _aim;
        private float _cooldown;
        private float _readyAt = float.NegativeInfinity;
        private VoxelAirStrike _strike;

        public AirStrikeBarrageModule(AimProvider aim, float cooldown)
        {
            _aim = aim;
            _cooldown = Mathf.Max(0f, cooldown);
        }

        public ModuleId Id => ModuleIds.SpecialSmoke;
        public bool IsReady => Time.time >= _readyAt;
        public float Remaining => Mathf.Max(0f, _readyAt - Time.time);
        public float Charge01 => _cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(Remaining / _cooldown);

        public float Cooldown
        {
            get => _cooldown;
            set => _cooldown = Mathf.Max(0f, value);
        }

        public bool TryActivate()
        {
            if (!IsReady || _aim == null || !_aim(out Vector3 point))
                return false;

            if (_strike == null)
                _strike = Object.FindFirstObjectByType<VoxelAirStrike>();
            if (_strike == null)
            {
                Debug.LogWarning("[AirStrikeBarrage] No VoxelAirStrike in the level: no bombs.");
                return false;
            }

            _readyAt = Time.time + _cooldown;
            _strike.StartCoroutine(Drop(_strike, point));
            return true;
        }

        private static IEnumerator Drop(VoxelAirStrike strike, Vector3 point)
        {
            WeaponSettings settings = CombatConfig.Load().Weapons;
            var wait = new WaitForSeconds(INTERVAL);
            for (int i = 0; i < BOMBS; i++)
            {
                if (strike == null)
                    yield break;
                Vector2 scatter = Random.insideUnitCircle * SCATTER;
                strike.Strike(point + new Vector3(scatter.x, 0f, scatter.y), settings.airStrikeDamage,
                    settings.airStrikeRadius, settings.airStrikeMinFactor);
                yield return wait;
            }
        }
    }
}
