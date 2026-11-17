using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Horde
{
    /// <summary>
    /// A* over a <see cref="HordeNavGrid"/> (same step rule as the flow field) for the few walkers that
    /// need their own way, a few searches a second. The result is a list of waypoints with surface
    /// heights, pulled tight by grid line of sight: the first is the start, the last the goal (or the
    /// reachable cell closest to it, <see cref="HordePathStatus.Partial"/>).
    /// <para>One search at a time; <see cref="Path"/> is owned by the finder and rewritten by the next search.</para>
    /// </summary>
    public sealed class HordePathfinder : IDisposable
    {
        private const int RESULT_STATUS = 0;
        private const int RESULT_EXPANDED = 1;

        private static readonly ProfilerMarker PathMarker = new("Horde.Path");

        private readonly HordeNavGrid _grid;
        private NativeArray<float> _heights;
        private NativeArray<byte> _edges;
        private NativeArray<int> _g;
        private NativeArray<int> _f;
        private NativeArray<int> _parent;
        private NativeArray<int> _stamp;
        private NativeArray<int> _heapItems;
        private NativeArray<int> _heapPos;
        private NativeArray<int> _result;
        private NativeList<int> _cells;
        private NativeList<float3> _path;
        private JobHandle _handle;
        private bool _running;
        private int _generation;
        private int _snapshotVersion = int.MinValue;
        private HordePathStatus _status;
        private int _expanded;

        public HordePathfinder(HordeNavGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            int cells = grid.CellCount;
            _heights = new NativeArray<float>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _edges = new NativeArray<byte>(cells, Allocator.Persistent);
            _g = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _f = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _parent = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _stamp = new NativeArray<int>(cells, Allocator.Persistent);
            _heapItems = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _heapPos = new NativeArray<int>(cells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            _result = new NativeArray<int>(2, Allocator.Persistent);
            _cells = new NativeList<int>(256, Allocator.Persistent);
            _path = new NativeList<float3>(32, Allocator.Persistent);
        }

        public bool IsRunning => _running;

        /// <summary>Outcome of the last completed search.</summary>
        public HordePathStatus Status => _status;

        /// <summary>Waypoints of the last completed search (start first). Read only between searches.</summary>
        public NativeList<float3> Path => _path;

        /// <summary>Cells the last search took off the open list.</summary>
        public int ExpandedCells => _expanded;

        /// <summary>Starts a search on a worker. Returns the running handle if one is already going.</summary>
        public JobHandle Schedule(float3 start, float3 goal, JobHandle dependsOn = default)
        {
            if (_running)
                return _handle;

            using (PathMarker.Auto())
            {
                HordeNavGridSettings settings = _grid.Settings;
                JobHandle handle = dependsOn;
                if (_snapshotVersion != _grid.Version)
                {
                    _heights.CopyFrom(_grid.Heights);
                    _snapshotVersion = _grid.Version;
                    handle = new HordeEdgeMaskJob
                    {
                        Heights = _heights,
                        Edges = _edges,
                        Size = settings.Size,
                        MaxStep = settings.MaxStep
                    }.Schedule(_edges.Length, 1024, handle);
                }

                if (++_generation == int.MaxValue)
                {
                    _generation = 1;
                    for (int i = 0; i < _stamp.Length; i++)
                        _stamp[i] = 0;
                }

                _handle = new HordeAStarJob
                {
                    Heights = _heights,
                    Edges = _edges,
                    G = _g,
                    F = _f,
                    Parent = _parent,
                    Stamp = _stamp,
                    HeapItems = _heapItems,
                    HeapPos = _heapPos,
                    Cells = _cells,
                    Path = _path,
                    Result = _result,
                    Generation = _generation,
                    Size = settings.Size,
                    Origin = settings.Origin,
                    CellSize = settings.CellSize,
                    Start = _grid.ClampCell(start),
                    Goal = _grid.ClampCell(goal),
                    StartPosition = start,
                    GoalPosition = goal
                }.Schedule(handle);

                JobHandle.ScheduleBatchedJobs();
                _running = true;
                return _handle;
            }
        }

        /// <summary>Waits for the running search (if any) and publishes its result.</summary>
        public HordePathStatus Complete()
        {
            if (!_running)
                return _status;

            using (PathMarker.Auto())
            {
                _handle.Complete();
                _running = false;
                _status = (HordePathStatus)_result[RESULT_STATUS];
                _expanded = _result[RESULT_EXPANDED];
                return _status;
            }
        }

        /// <summary>Search and wait in one call (tools, tests, or when the path is needed right now).</summary>
        public HordePathStatus FindPath(float3 start, float3 goal)
        {
            Complete();
            Schedule(start, goal);
            return Complete();
        }

        public void Dispose()
        {
            _handle.Complete();
            _running = false;
            if (_heights.IsCreated) _heights.Dispose();
            if (_edges.IsCreated) _edges.Dispose();
            if (_g.IsCreated) _g.Dispose();
            if (_f.IsCreated) _f.Dispose();
            if (_parent.IsCreated) _parent.Dispose();
            if (_stamp.IsCreated) _stamp.Dispose();
            if (_heapItems.IsCreated) _heapItems.Dispose();
            if (_heapPos.IsCreated) _heapPos.Dispose();
            if (_result.IsCreated) _result.Dispose();
            if (_cells.IsCreated) _cells.Dispose();
            if (_path.IsCreated) _path.Dispose();
        }
    }
}
