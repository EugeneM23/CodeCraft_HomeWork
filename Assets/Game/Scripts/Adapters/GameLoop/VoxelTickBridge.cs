using System;
using Game.Voxels;
using Ticking;
using UnityEngine;

namespace Game.GameLoop.Adapters
{
    /// <summary>
    /// Ticks the voxel components (<see cref="VoxelTicks"/>) from the game loop and runs their game time from the
    /// loop's: while the bridge lives the voxels' own Unity methods stand idle, and a paused loop stands them still.
    /// After the Atomic entities, as before the loop: the voxels keep their order among themselves.
    /// </summary>
    [DefaultExecutionOrder(5000)]
    public sealed class VoxelTickBridge : IUpdateListener, IFixedUpdateListener, ILateUpdateListener, IPauseListener,
        IDisposable
    {
        private readonly IGameLoop _loop;
        private bool _disposed;

        public VoxelTickBridge(IGameLoop loop)
        {
            _loop = loop ?? throw new ArgumentNullException(nameof(loop));
            VoxelTicks.SetTime(loop.Time);
            VoxelTicks.Driven = true;
        }

        public void OnUpdate(float deltaTime)
        {
            VoxelTicks.SetTime(_loop.Time);
            VoxelTicks.Update(deltaTime);
        }

        public void OnFixedUpdate(float deltaTime) => VoxelTicks.FixedUpdate(deltaTime);

        public void OnLateUpdate(float deltaTime) => VoxelTicks.LateUpdate(deltaTime);

        // The voxel renderers draw from their own LateUpdate (dt 0) while the loop stands
        public void OnPause() => VoxelTicks.Paused = true;

        public void OnResume() => VoxelTicks.Paused = false;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            VoxelTicks.Driven = false;
            VoxelTicks.Paused = false;
        }
    }
}
