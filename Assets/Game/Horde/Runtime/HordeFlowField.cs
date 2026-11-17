using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Horde
{
    /// <summary>
    /// Integration field toward one target cell over a <see cref="HordeNavGrid"/>: the walking cost of
    /// every cell to the target (10 per straight step, 14 per diagonal) and the unit direction to the
    /// cheapest neighbour. One field steers the whole horde, whatever its size.
    /// <para>
    /// Double-buffered: <see cref="Schedule"/> computes into the back buffers on a worker while the
    /// front ones stay readable; <see cref="Complete"/> (start of the next frame) swaps them. The job
    /// reads its own snapshot of the grid heights, refreshed only when the grid's version changed.
    /// </para>
    /// </summary>
    public sealed class HordeFlowField : IDisposable
    {
        /// <summary>
        /// When nothing walkable is under the target (a hole, a rubble top), the nearest walkable cell within this many
        /// metres becomes the target instead.
        /// </summary>
        public const float FALLBACK_RADIUS = 3f;

        private static readonly ProfilerMarker FlowFieldMarker = new("Horde.FlowField");

        private readonly HordeNavGrid _grid;
        private NativeArray<float> _heights;
        private NativeArray<int> _distance;
        private NativeArray<int> _distanceBack;
        private NativeArray<float2> _direction;
        private NativeArray<float2> _directionBack;
        private NativeArray<byte> _edges;
        private NativeArray<int> _next;
        private NativeArray<int> _prev;
        private JobHandle _handle;
        private bool _running;
        private bool _hasField;
        private int _snapshotVersion = int.MinValue;
        private int2 _target;
        private int2 _targetBack;
        private int _gridVersion;
        private int _gridVersionBack;

        public HordeFlowField(HordeNavGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            int cells = grid.CellCount;
            _heights = new NativeArray<float>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _distance = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _distanceBack = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _direction = new NativeArray<float2>(cells, Allocator.Persistent);
            _directionBack = new NativeArray<float2>(cells, Allocator.Persistent);
            _edges = new NativeArray<byte>(cells, Allocator.Persistent);
            _next = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _prev = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for (int i = 0; i < cells; i++)
                _distance[i] = int.MaxValue;
        }

        /// <summary>A computation is on the workers.</summary>
        public bool IsRunning => _running;

        /// <summary>The front buffers hold a finished field.</summary>
        public bool HasField => _hasField;

        /// <summary>Target cell of the readable field.</summary>
        public int2 TargetCell => _target;

        /// <summary>Grid <see cref="HordeNavGrid.Version"/> the readable field was computed on.</summary>
        public int GridVersion => _gridVersion;

        /// <summary>
        /// The readable directions, one per grid cell, for jobs (read only). Valid until the next
        /// <see cref="Complete"/> swaps the buffers: a job reading it must be done before that.
        /// </summary>
        internal NativeArray<float2> Directions => _direction;

        /// <summary>
        /// Starts a field toward <paramref name="target"/> (clamped into the grid). Every walkable cell within
        /// <paramref name="seedRadius"/> metres of it is a target too (the ground under the tank's hull); if none is,
        /// the nearest walkable cell within <see cref="FALLBACK_RADIUS"/> is. While one is still running this does
        /// nothing and returns its handle.
        /// </summary>
        public JobHandle Schedule(float3 target, float seedRadius = 0f, JobHandle dependsOn = default)
        {
            if (_running)
                return _handle;

            using (FlowFieldMarker.Auto())
            {
                int2 size = _grid.Size;
                JobHandle handle = dependsOn;
                if (_snapshotVersion != _grid.Version)
                {
                    _heights.CopyFrom(_grid.Heights);
                    _snapshotVersion = _grid.Version;
                    handle = new HordeEdgeMaskJob
                    {
                        Heights = _heights,
                        Edges = _edges,
                        Size = size,
                        MaxStep = _grid.MaxStep
                    }.Schedule(_edges.Length, 1024, handle);
                }

                _targetBack = _grid.ClampCell(target);
                _gridVersionBack = _snapshotVersion;

                handle = new HordeFlowDistanceJob
                {
                    Edges = _edges,
                    Distance = _distanceBack,
                    Next = _next,
                    Prev = _prev,
                    Size = size,
                    Target = _targetBack,
                    SeedRadius = math.max(0, (int)(seedRadius / _grid.CellSize)),
                    FallbackRadius = (int)math.ceil(FALLBACK_RADIUS / _grid.CellSize)
                }.Schedule(handle);

                handle = new HordeFlowDirectionJob
                {
                    Edges = _edges,
                    Distance = _distanceBack,
                    Direction = _directionBack,
                    Size = size
                }.Schedule(_distanceBack.Length, 1024, handle);

                JobHandle.ScheduleBatchedJobs();
                _handle = handle;
                _running = true;
                return handle;
            }
        }

        /// <summary>Waits for the running field (if any) and makes it the readable one. True if swapped.</summary>
        public bool Complete()
        {
            if (!_running)
                return false;

            using (FlowFieldMarker.Auto())
            {
                _handle.Complete();
                _running = false;
                (_distance, _distanceBack) = (_distanceBack, _distance);
                (_direction, _directionBack) = (_directionBack, _direction);
                _target = _targetBack;
                _gridVersion = _gridVersionBack;
                _hasField = true;
                return true;
            }
        }

        /// <summary>Unit XZ direction to walk at <paramref name="position"/>; zero at the target, off the grid or where cut off.</summary>
        public float2 DirectionAt(float3 position) =>
            _grid.TryCell(position, out int2 cell) ? _direction[_grid.Index(cell)] : float2.zero;

        public float2 DirectionOf(int2 cell) => _grid.InBounds(cell) ? _direction[_grid.Index(cell)] : float2.zero;

        /// <summary>Walking distance to the target in metres; +infinity where cut off or off the grid.</summary>
        public float DistanceAt(float3 position) =>
            _grid.TryCell(position, out int2 cell) ? DistanceOf(cell) : float.PositiveInfinity;

        public float DistanceOf(int2 cell)
        {
            if (!_grid.InBounds(cell))
                return float.PositiveInfinity;
            int cost = _distance[_grid.Index(cell)];
            return cost == int.MaxValue
                ? float.PositiveInfinity
                : cost * _grid.CellSize / HordeNavRules.ORTHOGONAL_COST;
        }

        /// <summary><paramref name="position"/> is in the target cell.</summary>
        public bool IsReached(float3 position) =>
            _hasField && _grid.TryCell(position, out int2 cell) && math.all(cell == _target);

        /// <summary>No walkable way from <paramref name="position"/> to the target (or no field yet).</summary>
        public bool IsUnreachable(float3 position) =>
            !_grid.TryCell(position, out int2 cell) || IsUnreachable(cell);

        public bool IsUnreachable(int2 cell) =>
            !_grid.InBounds(cell) || _distance[_grid.Index(cell)] == int.MaxValue;

        public void Dispose()
        {
            _handle.Complete();
            _running = false;
            if (_heights.IsCreated) _heights.Dispose();
            if (_distance.IsCreated) _distance.Dispose();
            if (_distanceBack.IsCreated) _distanceBack.Dispose();
            if (_direction.IsCreated) _direction.Dispose();
            if (_directionBack.IsCreated) _directionBack.Dispose();
            if (_edges.IsCreated) _edges.Dispose();
            if (_next.IsCreated) _next.Dispose();
            if (_prev.IsCreated) _prev.Dispose();
        }
    }
}
