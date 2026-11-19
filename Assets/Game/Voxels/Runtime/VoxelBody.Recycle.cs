using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Game.Voxels
{
    /// <summary>
    /// Reusing a body instead of making a new one (a creature's carcass, a building's piece): a new
    /// body costs a GameObject, chunk objects, meshes, renderers and box colliders — 20–30 KB of
    /// garbage in the frame it is made. Recycle puts a body away with all of that kept (switched
    /// off); Init on it takes the chunks back by index and the arrays handed out by TakeVoxels.
    /// </summary>
    public sealed partial class VoxelBody
    {
        /// <summary>
        /// Where pieces breaking off (Detach, Fracture) come from: a pool's recycled body (on its own
        /// GameObject, which is switched on, placed and given the piece layer), or null for a new one.
        /// </summary>
        public static Func<VoxelBody> PieceFactory;

        private bool _recycled;

        /// <summary>Put away by Recycle and not yet Init again.</summary>
        public bool IsRecycled => _recycled;

        /// <summary>
        /// Takes the body out of the world for reuse: off every queue (split, load solve, the async
        /// remesh), its chunks switched off with their meshes, renderers and colliders kept, its
        /// Rigidbody (if any) kinematic and still, its state back to a new body's. The voxel and
        /// palette arrays stay for TakeVoxels / TakePalette. The GameObject itself is the pool's.
        /// </summary>
        public void Recycle()
        {
            if (_recycled)
                return;

            VoxelWorld.Unregister(this);
            VoxelWorld.DropJobsOf(this);
            ReleaseStructure();

            if (_rigidbody != null)
            {
                if (!_rigidbody.isKinematic)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                }

                _rigidbody.isKinematic = true;
            }

            if (TryGetComponent(out VoxelImpactFracture fracture))
                fracture.ResetForReuse();

            if (_chunks != null)
                foreach (Chunk chunk in _chunks)
                    if (chunk != null && chunk.GameObject != null)
                        chunk.GameObject.SetActive(false);

            _activatePending = false;
            _pendingVelocity = Vector3.zero;
            _pendingSpin = Vector3.zero;
            _pendingBlastForce = 0f;
            _splitPending = false;
            _splitRegions = 0;
            _splitForce = 0f;
            _stained = 0;
            _looseCount = 0;
            _built = false;
            _dirtyCount = 0;
            _rebuildNowFrame = -1;
            Generation = 0;
            SolidCount = 0;
            Array.Clear(_loose, 0, _loose.Length);
            Array.Clear(_print, 0, _print.Length);
            _recycled = true;
        }

        /// <summary>
        /// A voxel array of this length for the next Init: a recycled body's own (cleared) when it is
        /// that long, otherwise a new one. The body owns it again once Init is given it.
        /// </summary>
        public NativeArray<byte> TakeVoxels(int length)
        {
            if (_recycled && _voxels.IsCreated && _voxels.Length == length)
            {
                new ClearBytesJob { Bytes = _voxels }.Run();

                return _voxels;
            }

            return new NativeArray<byte>(length, Allocator.Persistent);
        }

        /// <summary>A palette of this length for the next Init: a recycled body's own when it fits, else a new one.</summary>
        public NativeArray<Color32> TakePalette(int length)
        {
            if (_recycled && _palette.IsCreated && _palette.Length == length)
                return _palette;
            return new NativeArray<Color32>(length, Allocator.Persistent);
        }

        /// <summary>Init on a recycled body: its old arrays go unless they are the ones handed back.</summary>
        private void ReleaseRecycled(NativeArray<byte> voxels, NativeArray<Color32> palette)
        {
            if (_voxels.IsCreated && !SameArray(_voxels, voxels))
                _voxels.Dispose();
            if (_palette.IsCreated && !SameArray(_palette, palette))
                _palette.Dispose();
            _voxels = default;
            _palette = default;
        }

        /// <summary>The same buffer (NativeArray equality is by buffer and length).</summary>
        private static bool SameArray<T>(NativeArray<T> a, NativeArray<T> b) where T : struct =>
            a.IsCreated && b.IsCreated && a.Equals(b);

        /// <summary>Destroys chunk objects and their meshes (a re-Init with another layout).</summary>
        private void DestroyChunks(Chunk[] chunks = null)
        {
            chunks ??= _chunks;
            if (chunks == null)
                return;

            foreach (Chunk chunk in chunks)
            {
                if (chunk == null)
                    continue;
                if (chunk.Mesh != null)
                    Destroy(chunk.Mesh);
                if (chunk.CollisionA != null)
                    Destroy(chunk.CollisionA);
                if (chunk.CollisionB != null)
                    Destroy(chunk.CollisionB);
                if (chunk.GameObject != null)
                    Destroy(chunk.GameObject);
            }

            if (chunks == _chunks)
                _chunks = null;
        }
    }
}
