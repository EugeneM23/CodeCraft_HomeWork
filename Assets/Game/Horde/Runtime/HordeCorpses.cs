using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Horde
{
    /// <summary>
    /// Dead monsters lying on the ground: a baked static mesh of the death pose drawn instanced. Each
    /// lies <see cref="LieTime"/> s, then sinks into the ground over <see cref="SinkTime"/> s (in the
    /// shader) and its slot is freed. A ring of fixed capacity: corpses are added in time order, so
    /// the oldest is always at the head; when full, the oldest is recycled. Uploads only new slots.
    /// </summary>
    public sealed class HordeCorpses : IDisposable
    {
        private readonly HordeRenderer _renderer;
        private NativeArray<HordeInstance> _slots;
        private int _head;
        private int _count;
        private int _dirtyStart = -1;
        private int _dirtyCount;

        public HordeCorpses(Mesh mesh, Material material, int capacity, float lieTime = 6f, float sinkTime = 3f, float sinkDepth = 1.2f)
        {
            Capacity = Mathf.Max(1, capacity);
            LieTime = lieTime;
            SinkTime = sinkTime;
            SinkDepth = sinkDepth;
            _slots = new NativeArray<HordeInstance>(Capacity, Allocator.Persistent);
            _renderer = new HordeRenderer(mesh, material, Capacity, true);
            _renderer.SetCorpseTimes(lieTime, sinkTime, sinkDepth);
        }

        public int Capacity { get; }
        public int Count => _count;

        /// <summary>Corpses run over (<see cref="Squash"/>) since this ring was made.</summary>
        public int SquashedTotal { get; private set; }
        public float LieTime { get; }
        public float SinkTime { get; }
        public float SinkDepth { get; }

        public bool CastShadows
        {
            get => _renderer.CastShadows;
            set => _renderer.CastShadows = value;
        }

        /// <summary>Model scale and tint of this ring's corpses (see <see cref="HordeRenderer.SetLook"/>).</summary>
        public void SetLook(float scale, Color tint, Color emission) => _renderer.SetLook(scale, tint, emission);

        /// <summary>A corpse at <paramref name="position"/> facing <paramref name="heading"/>, born at <paramref name="time"/>.</summary>
        public void Add(float3 position, float heading, float time)
        {
            if (_count == Capacity)
            {
                _head = (_head + 1) % Capacity;
                _count--;
            }

            int slot = (_head + _count) % Capacity;
            _slots[slot] = new HordeInstance
            {
                PositionHeading = new float4(position, heading),
                Animation = new float4(time, 0f, 0f, 0f)
            };
            _count++;

            if (_dirtyStart < 0)
            {
                _dirtyStart = slot;
                _dirtyCount = 0;
            }

            _dirtyCount = Mathf.Min(_dirtyCount + 1, Capacity);
        }

        /// <summary>Frees the corpses that have sunk by <paramref name="time"/> and uploads the new ones.</summary>
        public void Update(float time)
        {
            float life = LieTime + SinkTime;
            while (_count > 0 && time - _slots[_head].Animation.x > life)
            {
                _head = (_head + 1) % Capacity;
                _count--;
            }

            if (_dirtyStart < 0)
                return;

            int first = Mathf.Min(_dirtyCount, Capacity - _dirtyStart);
            _renderer.Upload(_slots, _dirtyStart, _dirtyStart, first);
            if (_dirtyCount > first)
                _renderer.Upload(_slots, 0, 0, _dirtyCount - first);
            _dirtyStart = -1;
            _dirtyCount = 0;
        }

        /// <summary>
        /// Corpses still lying whose position is inside the box (centre, rotation, half extents, widened by
        /// <paramref name="margin"/>) are gone at once: sunk out of sight (their slot frees in turn). Their positions
        /// go into <paramref name="squashed"/> (cleared first), for the burst. Nothing allocated.
        /// </summary>
        public int Squash(float3 center, quaternion rotation, float3 halfExtents, float margin, float time,
            System.Collections.Generic.List<Vector3> squashed)
        {
            squashed.Clear();
            quaternion inverse = math.inverse(rotation);
            float3 reach = halfExtents + margin;
            float gone = time - LieTime - SinkTime;
            for (int i = 0; i < _count; i++)
            {
                int slot = (_head + i) % Capacity;
                HordeInstance corpse = _slots[slot];
                if (time - corpse.Animation.x > LieTime)
                    continue; // already sinking
                float3 local = math.mul(inverse, corpse.PositionHeading.xyz - center);
                if (math.any(math.abs(local) > reach))
                    continue;

                corpse.Animation.x = gone;
                _slots[slot] = corpse;
                _renderer.Upload(_slots, slot, slot, 1);
                squashed.Add(corpse.PositionHeading.xyz);
                SquashedTotal++;
            }

            return squashed.Count;
        }

        public void Draw(Bounds worldBounds, float time) => _renderer.Draw(_count, worldBounds, _head, time);

        public void Clear()
        {
            _head = 0;
            _count = 0;
            _dirtyStart = -1;
            _dirtyCount = 0;
        }

        public void Dispose()
        {
            _renderer.Dispose();
            if (_slots.IsCreated)
                _slots.Dispose();
        }
    }
}
