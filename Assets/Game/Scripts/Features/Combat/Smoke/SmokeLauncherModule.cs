using Game.Voxels;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Smoke launchers, the Tiger's special module on E (GDD §6): a wall of voxel smoke round the tank — a ring of
    /// eight overlapping clouds 4.5 m out and a larger one on the tank — standing <see cref="Duration"/> seconds, then
    /// <see cref="Cooldown"/> seconds to reload. The smoke is registered in the level's <see cref="SmokeScreen"/> as
    /// one circle of <see cref="COVER_RADIUS"/>, so spitters and the elite lose the tank.
    /// Tank key: SpecialModule. Game time (GameTicks.Time).
    /// </summary>
    /// <remarks>
    /// Sizes: a <see cref="VoxelSmoke"/> cloud of radius R fills, in the open, half an ellipsoid R wide and R / 1.35
    /// high in 30 cm cells (R 3 m ≈ 1550 cells, 2.2 m high), growing for 1.5 s and thinning away over its last 3.5 s.
    /// The ring clouds (R 3 m at 4.5 m) touch each other 3.4 m apart and reach from 1.5 m to 7.5 m (their rim thin), so
    /// the solid wall stands 2–7 m round the tank, matching the AI's 7 m circle; the centre cloud (R 3.5 m, 2.6 m high)
    /// hides the tank itself. Nine clouds draw ≈ 15 000 cubes, about what the old seven of R 3.2 m drew; VoxelSmoke has
    /// no cap on clouds, but every bullet cuts every active cloud, so the count is kept to nine.
    /// </remarks>
    public sealed class SmokeLauncherModule : ISpecialModule
    {
        /// <summary>Clouds in the ring.</summary>
        public const int RING_CLOUDS = 8;

        /// <summary>Radius (m) of the ring the clouds land on.</summary>
        public const float RING_RADIUS = 4.5f;

        /// <summary>Radius (m) of one ring cloud.</summary>
        public const float CLOUD_RADIUS = 3f;

        /// <summary>Radius (m) of the cloud on the tank: higher than the ring, over the turret.</summary>
        public const float CENTRE_CLOUD_RADIUS = 3.5f;

        /// <summary>Radius (m) the whole screen covers for the AI: the ring and its clouds' reach.</summary>
        public const float COVER_RADIUS = 7f;

        /// <summary>
        /// Seconds a cloud lives past <see cref="Duration"/>: VoxelSmoke thins a cloud away over the last 3.5 s of its
        /// lifetime, so the wall stands thick for the whole duration and thins after the AI's cover ends.
        /// </summary>
        public const float FADE_TAIL = 3.5f;

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
            float groundY = centre.y;
            Deploy(centre, CENTRE_CLOUD_RADIUS, groundY);

            // Turned with the hull, so the gaps between the clouds don't always face the same way.
            float yaw = _tank.eulerAngles.y * Mathf.Deg2Rad;
            for (int i = 0; i < RING_CLOUDS; i++)
            {
                float angle = yaw + (i + 0.5f) * (Mathf.PI * 2f / RING_CLOUDS);
                Deploy(centre + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * RING_RADIUS, CLOUD_RADIUS, groundY);
            }

            _screen?.Add(centre, COVER_RADIUS, _duration);
            CombatSignals.RaiseSmokeDeployed(centre, COVER_RADIUS);
            return true;
        }

        /// <summary>
        /// A cloud on the ground under the spot; without ground there (off the terrain's layer), at the tank's
        /// ground height, never up in the air or under a slope.
        /// </summary>
        private void Deploy(Vector3 spot, float radius, float groundY)
        {
            spot.y = groundY;
            if (Physics.Raycast(spot + Vector3.up * GROUND_PROBE, Vector3.down, out RaycastHit ground, GROUND_PROBE * 2f,
                    1 << PhysicsLayers.Ground, QueryTriggerInteraction.Ignore))
                spot = ground.point;

            VoxelSmoke.Deploy(spot + Vector3.up * 0.1f, radius, _duration + FADE_TAIL);
        }
    }
}
