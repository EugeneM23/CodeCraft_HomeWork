using System;
using Game.Horde;
using Unity.Mathematics;

namespace Game.AI
{
    /// <summary>
    /// One A* (<see cref="HordePathfinder"/>) shared by every walker of the level that needs its own way: the search
    /// buffers are as big as the grid, so one set serves everyone. Requests wait in a ring; one search runs on a worker
    /// at a time, scheduled in one frame and taken in at the next <see cref="Pump"/>. A client has at most one request:
    /// asking again only moves its start and goal. No managed allocations after construction.
    /// </summary>
    public sealed class LevelPathService : IDisposable
    {
        private const int CAPACITY = 64;

        private readonly HordePathfinder _finder;
        private readonly HordeNavGrid _grid;
        private readonly PathRequest[] _queue = new PathRequest[CAPACITY];
        private int _head;
        private int _count;
        private IPathClient _running;
        private bool _runningCancelled;

        private struct PathRequest
        {
            public IPathClient Client;
            public float3 Start;
            public float3 Goal;
        }

        public LevelPathService(HordeNavGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _finder = new HordePathfinder(grid);
        }

        /// <summary>On — a request is searched at once on the calling thread (tests, tools); off — one search per frame.</summary>
        public bool Synchronous { get; set; }

        /// <summary>Requests waiting (the running one not counted).</summary>
        public int Pending => _count;

        public bool IsRunning => _running != null;

        public HordeNavGrid Grid => _grid;

        /// <summary>Asks for a path. False — the ring is full (the client asks again later).</summary>
        public bool Request(IPathClient client, float3 start, float3 goal)
        {
            if (client == null)
                return false;

            if (Synchronous)
            {
                HordePathStatus status = _finder.FindPath(start, goal);
                client.OnPathFound(status, _finder.Path, _grid.Version);
                return true;
            }

            for (int i = 0; i < _count; i++)
            {
                int index = (_head + i) % CAPACITY;
                if (!ReferenceEquals(_queue[index].Client, client))
                    continue;
                _queue[index].Start = start;
                _queue[index].Goal = goal;
                return true;
            }

            if (_count == CAPACITY)
                return false;

            _queue[(_head + _count) % CAPACITY] = new PathRequest { Client = client, Start = start, Goal = goal };
            _count++;
            if (_running == null)
                ScheduleNext();
            return true;
        }

        /// <summary>Forgets the client's request (a running search finishes, its result goes nowhere).</summary>
        public void Cancel(IPathClient client)
        {
            if (client == null)
                return;
            if (ReferenceEquals(_running, client))
                _runningCancelled = true;

            for (int i = 0; i < _count; i++)
            {
                int index = (_head + i) % CAPACITY;
                if (!ReferenceEquals(_queue[index].Client, client))
                    continue;
                for (int k = i; k < _count - 1; k++)
                    _queue[(_head + k) % CAPACITY] = _queue[(_head + k + 1) % CAPACITY];
                _queue[(_head + _count - 1) % CAPACITY] = default;
                _count--;
                return;
            }
        }

        /// <summary>
        /// Once a frame: takes in the search scheduled last frame (it had the whole frame on a worker), hands its result
        /// to its client and schedules the next request.
        /// </summary>
        public void Pump()
        {
            if (_running != null)
            {
                HordePathStatus status = _finder.Complete();
                IPathClient client = _running;
                bool cancelled = _runningCancelled;
                _running = null;
                _runningCancelled = false;
                if (!cancelled)
                    client.OnPathFound(status, _finder.Path, _grid.Version);
            }

            if (_running == null)
                ScheduleNext();
        }

        public void Dispose()
        {
            _running = null;
            _count = 0;
            _finder.Dispose();
        }

        private void ScheduleNext()
        {
            if (_count == 0)
                return;

            PathRequest next = _queue[_head];
            _queue[_head] = default;
            _head = (_head + 1) % CAPACITY;
            _count--;
            _running = next.Client;
            _runningCancelled = false;
            _finder.Schedule(next.Start, next.Goal);
        }
    }
}
