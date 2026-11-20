using System.Collections.Generic;
using Game.Voxels;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Zombies in the houses (the user): every room of every voxel building holds a stock of them (just a count, no
    /// zombie exists before), and a shell or a ram that opens a hole through an outer wall into a room with zombies left
    /// sets them pouring out through it: a stream of horde crawlers at the foot of the wall under the hole, a steady
    /// <see cref="RATE"/> a second, until the room is empty; holes into one room share its stock. Breaches come from the
    /// carves themselves (VoxelSignals.Carved): no house is scanned. The sandbox's (SandboxHordeBehaviour ticks it).
    /// </summary>
    /// <remarks>
    /// The horde walks a height grid: it has no inside of a house. A stream is born just outside the wall under the hole,
    /// on the ground, and walks out from there: from the ground floor that reads as pouring out of it; from upper floors
    /// the zombies appear at the foot of the wall.
    /// </remarks>
    public sealed class HouseZombies
    {
        /// <summary>The debug panel's «Зомби в домах».</summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>Zombies a room holds at the start.</summary>
        public static int PerRoom { get; set; } = 200;

        /// <summary>Zombies a second out of one hole.</summary>
        public const float RATE = 20f;

        /// <summary>A carve narrower than this (m) is no way out for a body.</summary>
        private const float MIN_HOLE = 0.8f;

        /// <summary>How far through a wall (m, from the room's inside face) a carve still opens it.</summary>
        private const float WALL_DEPTH = 1.1f;

        /// <summary>Holes nearer than this (m) to one of the room's are the same hole.</summary>
        private const float SAME_HOLE = 1.2f;

        /// <summary>A stream is born this far (m) outside the wall, and spread this much along it.</summary>
        private const float OUTSIDE = 0.6f;
        private const float SPREAD = 0.5f;

        private sealed class Room
        {
            public Transform House;
            public Bounds Inside;
            public int Outer;
            public int Stock;
        }

        private sealed class Hole
        {
            public Room Room;
            public Vector3 Out;
            public Vector3 Along;
            public float Carry;
        }

        private readonly HordeBehaviour _horde;
        private readonly List<Room> _rooms = new(64);
        private readonly List<Hole> _holes = new(32);
        private readonly List<VoxelStructure.Room> _scratch = new(8);
        private readonly System.Action<Vector3, float, int> _onCarved;
        private bool _built;

        public HouseZombies(HordeBehaviour horde)
        {
            _horde = horde;
            _onCarved = OnCarved;
            VoxelSignals.Carved += _onCarved;
        }

        public void Dispose() => VoxelSignals.Carved -= _onCarved;

        /// <summary>Rooms with zombies left, and zombies left in all of them.</summary>
        public int RoomsLeft { get; private set; }
        public int ZombiesLeft { get; private set; }

        /// <summary>Holes pouring now.</summary>
        public int Streams { get; private set; }

        /// <summary>The houses' rooms and their stocks, once: the buildings stand by then.</summary>
        private void Build()
        {
            _built = true;
            foreach (VoxelStructure house in Object.FindObjectsByType<VoxelStructure>(FindObjectsSortMode.None))
            {
                house.GetRooms(_scratch);
                foreach (VoxelStructure.Room r in _scratch)
                    _rooms.Add(new Room { House = house.transform, Inside = r.Inside, Outer = r.Outer, Stock = PerRoom });
            }

            Count();
        }

        private void OnCarved(Vector3 point, float radius, int voxels)
        {
            if (!Enabled || radius * 2f < MIN_HOLE || voxels <= 0)
                return;
            if (!_built)
                Build();

            for (int i = 0; i < _rooms.Count; i++)
            {
                Room room = _rooms[i];
                if (room.Stock <= 0 || room.House == null)
                    continue;
                Vector3 local = room.House.InverseTransformPoint(point);
                if (!TryWall(room, local, radius, out Vector3 normal, out Vector3 onWall))
                    continue;

                Vector3 outWorld = room.House.TransformPoint(onWall + normal * OUTSIDE);
                bool known = false;
                for (int h = 0; h < _holes.Count; h++)
                    if (_holes[h].Room == room && (_holes[h].Out - outWorld).sqrMagnitude < SAME_HOLE * SAME_HOLE)
                        known = true;
                if (!known)
                    _holes.Add(new Hole
                    {
                        Room = room,
                        Out = outWorld,
                        Along = room.House.TransformDirection(Vector3.Cross(Vector3.up, normal)).normalized
                    });
            }
        }

        /// <summary>
        /// The carve (a sphere at <paramref name="local"/>, house space) went through one of the room's outer walls:
        /// within the wall's depth outside its inside face, over the face, reaching its inside. Its outward normal and the
        /// point on the inside face.
        /// </summary>
        private static bool TryWall(Room room, Vector3 local, float radius, out Vector3 normal, out Vector3 onWall)
        {
            normal = Vector3.zero;
            onWall = Vector3.zero;
            Bounds b = room.Inside;
            Vector3 min = b.min, max = b.max;
            // Over the room's height and its wall's run (the carve's reach counts).
            if (local.y + radius < min.y || local.y - radius > max.y)
                return false;

            float best = float.MaxValue;
            for (int face = 0; face < 4; face++)
            {
                if ((room.Outer & (1 << face)) == 0)
                    continue;
                bool alongX = face >= 2; // -Z / +Z faces run along X
                float plane = face switch { 0 => min.x, 1 => max.x, 2 => min.z, _ => max.z };
                float sign = face % 2 == 0 ? -1f : 1f;
                float across = alongX ? local.z : local.x;
                float run = alongX ? local.x : local.z;
                float runMin = alongX ? min.x : min.z, runMax = alongX ? max.x : max.z;
                // Outside the inside face by up to the wall's depth, and the sphere reaches in past it.
                float outward = (across - plane) * sign;
                if (outward < -radius || outward > WALL_DEPTH || outward - radius > 0f)
                    continue;
                if (run < runMin - radius * 0.5f || run > runMax + radius * 0.5f)
                    continue;
                if (Mathf.Abs(outward) >= best)
                    continue;
                best = Mathf.Abs(outward);
                normal = alongX ? new Vector3(0f, 0f, sign) : new Vector3(sign, 0f, 0f);
                float y = Mathf.Clamp(local.y, min.y, max.y);
                float r = Mathf.Clamp(run, runMin, runMax);
                onWall = alongX ? new Vector3(r, y, plane) : new Vector3(plane, y, r);
            }

            return best < float.MaxValue;
        }

        /// <summary>The streams: <see cref="RATE"/> a second out of every hole into a room with zombies left.</summary>
        public void Tick(float deltaTime)
        {
            if (!_built)
                Build();
            if (!Enabled)
                return;
            int streams = 0;
            for (int h = _holes.Count - 1; h >= 0; h--)
            {
                Hole hole = _holes[h];
                if (hole.Room.Stock <= 0)
                {
                    _holes.RemoveAt(h);
                    continue;
                }

                streams++;
                hole.Carry += RATE * deltaTime;
                while (hole.Carry >= 1f && hole.Room.Stock > 0)
                {
                    Vector3 at = hole.Out + hole.Along * Random.Range(-SPREAD, SPREAD);
                    if (!_horde.SpawnWalking(at))
                        break; // the horde is full or its queue is: next tick
                    hole.Carry -= 1f;
                    hole.Room.Stock--;
                }

                hole.Carry = Mathf.Min(hole.Carry, 2f);
            }

            Streams = streams;
            if (streams > 0)
                Count();
        }

        private void Count()
        {
            int rooms = 0, zombies = 0;
            for (int i = 0; i < _rooms.Count; i++)
            {
                if (_rooms[i].Stock <= 0)
                    continue;
                rooms++;
                zombies += _rooms[i].Stock;
            }

            RoomsLeft = rooms;
            ZombiesLeft = zombies;
        }
    }
}
