using Atomic.Entities;
using Game.Horde;
using Unity.Mathematics;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The sandbox's live horde, on the horde entity next to <see cref="HordeBehaviour"/>: keeps the level grid over the
    /// whole sandbox map, releases a group of crawlers on open ground a short drive from the tank (they rise out of the
    /// ground, come at the tank and leap on it, HordeRiders), and a new group <see cref="SandboxHordeConfig.respawnDelay"/>
    /// after the last one is wiped out. Off (<see cref="SandboxHorde.Enabled"/>): the horde is cleared without deaths and
    /// nothing comes. No allocations per frame.
    /// </summary>
    public sealed class SandboxHordeBehaviour : IEntityInit, IEntityTick, IEntityDispose
    {
        private const int SPAWN_TRIES = 12;
        /// <summary>A spawn cell higher than the centre's ground by this is a roof or a ramp, not the grass, m.</summary>
        private const float MAX_RISE = 0.6f;

        private readonly SandboxHordeConfig _config;
        private readonly HordeBehaviour _horde;
        private Unity.Mathematics.Random _random = new(0x5A9D80u);
        private float _timer;
        private bool _wasOn = true;
        private HouseZombies _houses;

        /// <summary>The houses' zombies (HouseZombies): their stocks and streams.</summary>
        public HouseZombies Houses => _houses;

        public SandboxHordeBehaviour(SandboxHordeConfig config, HordeBehaviour horde)
        {
            _config = config;
            _horde = horde;
        }

        public HordeBehaviour Horde => _horde;

        public SandboxHordeConfig Config => _config;

        /// <summary>Monsters of the sandbox horde alive (asked-for ones included).</summary>
        public int Alive => _horde.AliveCount;

        /// <summary>Seconds until the next group, or -1 while one is still alive (or the horde is off).</summary>
        public float NextGroupIn => !SandboxHorde.Enabled || _horde.AliveCount > 0 ? -1f : math.max(_timer, 0f);

        /// <summary>Groups released so far.</summary>
        public int Groups { get; private set; }

        /// <summary>How many times the level grid was moved back over the map.</summary>
        public int NavRestores { get; private set; }

        public void Init(IEntity entity)
        {
            _timer = _config.firstDelay;
            _houses = new HouseZombies(_horde);
            SandboxHorde.SetActive(this);
        }

        public void Dispose(IEntity entity)
        {
            _houses?.Dispose();
            SandboxHorde.ClearActive(this);
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            KeepGridOverMap();

            if (!SandboxHorde.Enabled)
            {
                if (_wasOn)
                {
                    _horde.ResetForArena(); // gone at once, no deaths: no experience for switching it off
                    _wasOn = false;
                }

                _timer = _config.firstDelay;
                return;
            }

            _wasOn = true;
            _houses?.Tick(deltaTime);

            // The test hotkey (the user): H releases a group (SandboxHordeConfig.groupSize) at once.
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
                SpawnGroup();

            // Groups on their own only with SandboxHorde.AutoGroups (off by default: the crowd comes on H).
            if (!SandboxHorde.AutoGroups)
                return;

            if (_horde.AliveCount > 0)
            {
                _timer = _config.respawnDelay;
                return;
            }

            _timer -= deltaTime;
            if (_timer <= 0f)
                SpawnGroup();
        }

        /// <summary>Releases a group now (the debug panel), whatever is alive.</summary>
        public int SpawnGroup()
        {
            _timer = _config.respawnDelay;
            HordeNavGrid grid = _horde.Grid;
            if (grid == null)
                return 0;

            int size = SandboxHorde.GroupSize;
            float3 centre = _config.spawnCenter;
            float baseHeight = grid.HeightAt(centre);
            if (float.IsNaN(baseHeight))
                baseHeight = centre.y;
            HordeFlowField flow = _horde.FlowField;
            int spawned = 0;
            for (int n = 0; n < size; n++)
            {
                for (int attempt = 0; attempt < SPAWN_TRIES; attempt++)
                {
                    float2 offset = _random.NextFloat2Direction() * (_config.spawnRadius * math.sqrt(_random.NextFloat()));
                    var at = new float3(centre.x + offset.x, baseHeight, centre.z + offset.y);
                    float h = grid.HeightAt(at);
                    if (float.IsNaN(h) || h > baseHeight + MAX_RISE || h < baseHeight - 2f)
                        continue;
                    if (flow != null && flow.HasField && flow.IsUnreachable(at))
                        continue;
                    at.y = h;
                    if (_horde.Spawn(EnemyKind.Crawler, at, 1f, 1f))
                        spawned++;
                    break;
                }
            }

            if (spawned > 0)
                Groups++;
            return spawned;
        }

        /// <summary>
        /// The run's arena director moves the level grid over its arena (in the sandbox a 40 m square in the town's
        /// middle), which leaves the tank's start off the grid: the crowd then stops at the grid's edge. Moved back over
        /// the whole map when that happens (a load-time rebuild; the horde is cleared with it).
        /// </summary>
        private void KeepGridOverMap()
        {
            IEntity level = LevelAI.Current;
            if (level == null || !level.TryGetLevelNavigation(out LevelNav nav) || nav == null || !nav.IsBuilt)
                return;

            HordeNavGridSettings s = nav.Grid.Settings;
            float2 min = s.Origin;
            float2 max = s.Origin + (float2)s.Size * s.CellSize;
            const float slack = 0.5f;
            if (min.x <= _config.mapMin.x + slack && min.y <= _config.mapMin.y + slack
                && max.x >= _config.mapMax.x - slack && max.y >= _config.mapMax.y - slack)
                return;

            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(_config.mapMin.x, _config.groundBottom, _config.mapMin.y),
                new Vector3(_config.mapMax.x, _config.groundTop, _config.mapMax.y));
            nav.Rebuild(bounds, 0f);
            NavRestores++;
        }
    }
}
