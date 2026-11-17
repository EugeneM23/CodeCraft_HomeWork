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
        public float LieTime { get; }
        public float SinkTime { get; }
        public float SinkDepth { get; }

        public bool CastShadows
        {
            get => _renderer.CastShadows;
            set => _renderer.CastShadows = value;
        }

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
