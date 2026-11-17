using System;
using Game.Horde;
using Game.Voxels;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// The level's one navigation grid (<see cref="HordeNavGrid"/>) and the path service over it, shared by the horde's
    /// flow field and the elites' A*. Built on first use (a horde or an elite may ask before the level entity starts);
    /// listens to <see cref="VoxelWorld.ChunkCollidersChanged"/> and rereads the dug cells: rays scheduled in the level's
    /// tick (<see cref="ScheduleUpdate"/>), taken in at the start of the next frame (<see cref="LevelAIEarlyUpdate"/>,
    /// after the horde's step, which reads the heights on a worker). Every changed rect is read once more
    /// <see cref="REREAD_DELAY"/> later: a wall piece that broke off is still standing in the frame it broke, and the
    /// first read would keep it as a wall after it fell.
    /// </summary>
    public sealed class LevelNav : IDisposable
    {
        /// <summary>Seconds after a change at which its cells are read again (the broken-off pieces have fallen).</summary>
        public const float REREAD_DELAY = 0.5f;

        /// <summary>Second reads waiting at most; past this the newest one grows to cover the next change.</summary>
        private const int REREAD_CAPACITY = 32;

        private readonly LevelNavOptions _options;
        private readonly bool _ownsGrid;
        private readonly Bounds[] _rereadBounds = new Bounds[REREAD_CAPACITY];
        private readonly float[] _rereadAt = new float[REREAD_CAPACITY];
        private HordeNavGrid _grid;
        private LevelPathService _paths;
        private int _rereadHead;
        private int _rereadCount;
        private float _clock;
        private bool _hooked;
        private bool _disposed;

        /// <summary>A grid read from the level's colliders on first use, kept up to date with the digging.</summary>
        public LevelNav(LevelNavOptions options)
        {
            _options = options ?? new LevelNavOptions();
            _ownsGrid = true;
        }

        /// <summary>A grid made elsewhere (tests, tools): not listened to, not disposed with this.</summary>
        public LevelNav(HordeNavGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _paths = new LevelPathService(grid);
        }

        public LevelNavOptions Options => _options;

        public bool IsBuilt => _grid != null;

        /// <summary>Second reads waiting (tests, the debugger).</summary>
        public int PendingRereads => _rereadCount;

        /// <summary>The grid (read from the colliders the first time it is asked).</summary>
        public HordeNavGrid Grid
        {
            get
            {
                EnsureBuilt();
                return _grid;
            }
        }

        public LevelPathService Paths
        {
            get
            {
                EnsureBuilt();
                return _paths;
            }
        }

        /// <summary>Builds the grid now (load time: every cell's ray, waited for) and starts listening to the digging.</summary>
        public void EnsureBuilt()
        {
            if (_grid != null || _disposed)
                return;

            _grid = new HordeNavGrid(_options.ToSettings());
            _grid.BuildAll();
            _paths = new LevelPathService(_grid);
            VoxelWorld.ChunkCollidersChanged += OnChunkCollidersChanged;
            LevelAIEarlyUpdate.Add(this);
            _hooked = true;
        }

        /// <summary>
        /// Once a frame: queues the second reads that are due, then schedules rays over the cells dug since the last
        /// call (a no-op while rays are in flight).
        /// </summary>
        public void ScheduleUpdate(float deltaTime)
        {
            _clock += deltaTime;
            if (_grid == null)
                return;

            while (_rereadCount > 0 && _rereadAt[_rereadHead] <= _clock)
            {
                _grid.MarkDirty(_rereadBounds[_rereadHead]);
                _rereadHead = (_rereadHead + 1) % REREAD_CAPACITY;
                _rereadCount--;
            }

            if (_grid.DirtyRectCount > 0)
                _grid.ScheduleUpdate();
        }

        /// <summary>Takes in the rays of the last frame (start of the frame).</summary>
        public void CompleteUpdate() => _grid?.CompleteUpdate();

        /// <summary>Takes in the path search of the last frame and starts the next one.</summary>
        public void PumpPaths() => _paths?.Pump();

        /// <summary>A change of the ground under <paramref name="bounds"/>: read now and once more a little later.</summary>
        public void MarkChanged(Bounds bounds)
        {
            if (_grid == null)
                return;

            _grid.MarkDirty(bounds);
            float at = _clock + REREAD_DELAY;
            if (_rereadCount == REREAD_CAPACITY)
            {
                int newest = (_rereadHead + _rereadCount - 1) % REREAD_CAPACITY;
                _rereadBounds[newest].Encapsulate(bounds);
                _rereadAt[newest] = at;
                return;
            }

            int slot = (_rereadHead + _rereadCount) % REREAD_CAPACITY;
            _rereadBounds[slot] = bounds;
            _rereadAt[slot] = at;
            _rereadCount++;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_hooked)
            {
                VoxelWorld.ChunkCollidersChanged -= OnChunkCollidersChanged;
                LevelAIEarlyUpdate.Remove(this);
                _hooked = false;
            }

            _paths?.Dispose();
            _paths = null;
            if (_ownsGrid && _grid != null)
            {
                // The hordes' steps scheduled this frame read the heights on a worker: done before they are freed.
                LevelAIEarlyUpdate.CompleteHordes();
                _grid.Dispose();
            }

            _grid = null;
            _rereadCount = 0;
        }

        private void OnChunkCollidersChanged(Bounds bounds) => MarkChanged(bounds);
    }
}
