using System;
using Game.Horde;
using Game.Voxels;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// The level's one navigation grid (<see cref="HordeNavGrid"/>) and the path service over it, shared by the horde's
    /// flow field and the elites' A*. Built on first use (a horde or an elite may ask before the level entity starts);
    /// listens to <see cref="VoxelWorld.ChunkCollidersChanged"/> and rereads the dug cells: rays scheduled in the level's
    /// tick (<see cref="ScheduleUpdate"/>), taken in at the start of the next frame (<see cref="LevelAIEarlyUpdate"/>,
    /// after the horde's step, which reads the heights on a worker).
    /// </summary>
    public sealed class LevelNav : IDisposable
    {
        private readonly LevelNavOptions _options;
        private readonly bool _ownsGrid;
        private HordeNavGrid _grid;
        private LevelPathService _paths;
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

        /// <summary>Schedules rays over the cells dug since the last call (a no-op while rays are in flight).</summary>
        public void ScheduleUpdate()
        {
            if (_grid != null && _grid.DirtyRectCount > 0)
                _grid.ScheduleUpdate();
        }

        /// <summary>Takes in the rays of the last frame (start of the frame).</summary>
        public void CompleteUpdate() => _grid?.CompleteUpdate();

        /// <summary>Takes in the path search of the last frame and starts the next one.</summary>
        public void PumpPaths() => _paths?.Pump();

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
            if (_ownsGrid)
                _grid?.Dispose();
            _grid = null;
        }

        private void OnChunkCollidersChanged(Bounds bounds) => _grid?.MarkDirty(bounds);
    }
}
