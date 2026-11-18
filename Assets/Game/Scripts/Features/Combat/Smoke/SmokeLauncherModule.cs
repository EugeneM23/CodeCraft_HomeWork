using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Smoke launchers, the Tiger's special module on E (GDD §6): a ring of six voxel smoke clouds 4 m round the tank
    /// and one on it, for <see cref="Duration"/> seconds, then <see cref="Cooldown"/> seconds to reload. The smoke is
    /// registered in the level's <see cref="SmokeScreen"/> as one circle, so spitters and the elite lose the tank.
    /// Tank key: SpecialModule. Game time (GameTicks.Time).
    /// </summary>
    public sealed class SmokeLauncherModule : ISpecialModule
    {
        /// <summary>Clouds in the ring.</summary>
        public const int RING_CLOUDS = 6;

        /// <summary>Radius (m) of the ring the clouds land on.</summary>
        public const float RING_RADIUS = 4f;

        /// <summary>Radius (m) of one voxel cloud (VoxelSmoke's own default).</summary>
        public const float CLOUD_RADIUS = 3.2f;

        /// <summary>Radius (m) the whole screen covers for the AI: the ring and a cloud past it.</summary>
        public const float COVER_RADIUS = 7f;

        /// <summary>How far above and below a cloud's spot the ground is looked for.</summary>
        private const float GROUND_PROBE = 3f;

        private readonly Transform _tank;
        private readonly SmokeScreen _screen;
        private float _readyAt = float.NegativeInfinity;
        private float _duration;
        private float _cooldown;

        public SmokeLauncherModule(Transform tank, SmokeScreen screen, float duration, float cooldown)
        {
            _tank = tank;
            _screen = screen;
            Duration = duration;
            Cooldown = cooldown;
        }

        public ModuleId Id => ModuleIds.SpecialSmoke;

        /// <summary>The level's smoke screen the launcher reports to.</summary>
        public SmokeScreen Screen => _screen;

        /// <summary>Seconds the smoke stands; the loadout sets it.</summary>
        public float Duration
        {
            get => _duration;
            set => _duration = Mathf.Max(0.1f, value);
        }

        /// <summary>Seconds to reload after a use; the loadout sets it.</summary>
        public float Cooldown
        {
            get => _cooldown;
            set => _cooldown = Mathf.Max(0f, value);
        }

        public bool IsReady => GameTicks.Time >= _readyAt;

        public float Remaining => Mathf.Max(0f, _readyAt - GameTicks.Time);

        public float Charge01 => _cooldown <= 0f ? 1f : Mathf.Clamp01(1f - Remaining / _cooldown);

        public bool TryActivate()
        {
            if (!IsReady || _tank == null)
                return false;

            _readyAt = GameTicks.Time + _cooldown;

            Vector3 centre = _tank.position;
            Deploy(centre);
            for (int i = 0; i < RING_CLOUDS; i++)
            {
                float angle = i * (Mathf.PI * 2f / RING_CLOUDS);
                Deploy(centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * RING_RADIUS);
            }

            _screen?.Add(centre, COVER_RADIUS, _duration);
            CombatSignals.RaiseSmokeDeployed(centre, COVER_RADIUS);
            return true;
        }

        /// <summary>A cloud on the ground under the spot, or at the spot without ground.</summary>
        private void Deploy(Vector3 spot)
        {
            if (Physics.Raycast(spot + Vector3.up * GROUND_PROBE, Vector3.down, out RaycastHit ground, GROUND_PROBE * 2f,
                    1 << PhysicsLayers.Ground, QueryTriggerInteraction.Ignore))
                spot = ground.point + Vector3.up * 0.1f;

            VoxelSmoke.Deploy(spot, CLOUD_RADIUS, _duration);
        }
    }
}
