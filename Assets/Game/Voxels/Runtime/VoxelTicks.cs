using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Voxels
{
    /// <summary>
    /// Where the voxel components tick from. Every one joins in OnEnable and leaves in OnDisable. Without an owner
    /// (<see cref="Driven"/> false: tests, demo scenes) each component ticks itself from its own Unity method, as it
    /// always did. With an owner (the game loop's bridge) the Unity methods stand idle and the owner calls
    /// <see cref="Update"/>, <see cref="LateUpdate"/> and <see cref="FixedUpdate"/>: a paused game is the owner not
    /// calling them. Listeners run sorted by [DefaultExecutionOrder] of their class, then by the order they joined. One
    /// that leaves during a pass stops at once, one that joins waits for the next pass; an exception is logged and the
    /// rest go on. <see cref="Time"/> is the game time the voxels measure with instead of UnityEngine.Time.time.
    /// </summary>
    public static class VoxelTicks
    {
        private static readonly Dictionary<Type, int> Orders = new();
        private static readonly HashSet<object> Listeners = new(ReferenceComparer.Instance);
        private static readonly Phase<IVoxelUpdate> Updates = new("Update");
        private static readonly Phase<IVoxelLateUpdate> LateUpdates = new("LateUpdate");
        private static readonly Phase<IVoxelFixedUpdate> FixedUpdates = new("FixedUpdate");

        private static readonly Action<IVoxelUpdate, float> CallUpdate = static (l, dt) => l.VoxelUpdate(dt);
        private static readonly Action<IVoxelLateUpdate, float> CallLateUpdate = static (l, dt) => l.VoxelLateUpdate(dt);
        private static readonly Action<IVoxelFixedUpdate, float> CallFixedUpdate = static (l, dt) => l.VoxelFixedUpdate(dt);

        private static float s_time;

        /// <summary>An owner ticks the voxels; the components' own Unity methods do nothing.</summary>
        public static bool Driven { get; set; }

        /// <summary>
        /// The owner's game stands (pause). Components that draw from their late tick keep drawing then, with deltaTime 0,
        /// from their own Unity method: Graphics.DrawMesh* lasts only the frame it is called in.
        /// </summary>
        public static bool Paused { get; set; }

        /// <summary>Game seconds: the owner's time when driven, otherwise UnityEngine.Time.time.</summary>
        public static float Time => Driven ? s_time : UnityEngine.Time.time;

        public static int UpdateCount => Updates.Count;
        public static int LateUpdateCount => LateUpdates.Count;
        public static int FixedUpdateCount => FixedUpdates.Count;

        /// <summary>The owner's game time; read through <see cref="Time"/> while <see cref="Driven"/>.</summary>
        public static void SetTime(float time) => s_time = time;

        /// <summary>Joins by every voxel tick interface the object implements. Joining again does nothing.</summary>
        public static void Add(object listener)
        {
            if (listener == null || !Listeners.Add(listener))
                return;

            Type type = listener.GetType();
            int order = OrderOf(type);
            if (listener is IVoxelUpdate update)
                Updates.Add(update, order, type);
            if (listener is IVoxelLateUpdate lateUpdate)
                LateUpdates.Add(lateUpdate, order, type);
            if (listener is IVoxelFixedUpdate fixedUpdate)
                FixedUpdates.Add(fixedUpdate, order, type);
        }

        /// <summary>Leaves at once, even in the middle of a pass. Unknown objects are ignored.</summary>
        public static void Remove(object listener)
        {
            if (listener == null || !Listeners.Remove(listener))
                return;

            if (listener is IVoxelUpdate update)
                Updates.Remove(update);
            if (listener is IVoxelLateUpdate lateUpdate)
                LateUpdates.Remove(lateUpdate);
            if (listener is IVoxelFixedUpdate fixedUpdate)
                FixedUpdates.Remove(fixedUpdate);
        }

        public static bool Contains(object listener) => listener != null && Listeners.Contains(listener);

        public static void Update(float deltaTime) => Updates.Tick(deltaTime, CallUpdate);

        public static void LateUpdate(float deltaTime) => LateUpdates.Tick(deltaTime, CallLateUpdate);

        public static void FixedUpdate(float deltaTime) => FixedUpdates.Tick(deltaTime, CallFixedUpdate);

        /// <summary>Every listener leaves, no owner, time 0: the start of Play, tests.</summary>
        public static void Clear()
        {
            Listeners.Clear();
            Updates.Clear();
            LateUpdates.Clear();
            FixedUpdates.Clear();
            Driven = false;
            Paused = false;
            s_time = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Clear();

        private static int OrderOf(Type type)
        {
            if (Orders.TryGetValue(type, out int order))
                return order;

            var attribute = (DefaultExecutionOrder)Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder), true);
            order = attribute?.order ?? 0;
            Orders[type] = order;
            return order;
        }

        /// <summary>
        /// The listeners of one phase in tick order. A listener removed during a pass leaves a hole the pass skips; one
        /// added during a pass waits. Holes are swept and the waiting put in place when the outermost pass ends.
        /// </summary>
        private sealed class Phase<T> where T : class
        {
            private struct Entry
            {
                public T Listener;
                public int Order;
                public ProfilerMarker Marker;
            }

            private readonly string _name;
            private readonly Dictionary<Type, ProfilerMarker> _markers = new();
            private readonly List<Entry> _entries = new();
            private readonly List<Entry> _waiting = new();
            private int _passes;
            private bool _hasHoles;

            public Phase(string name) => _name = name;

            public int Count { get; private set; }

            public void Add(T listener, int order, Type type)
            {
                if (!_markers.TryGetValue(type, out ProfilerMarker marker))
                    _markers[type] = marker = new ProfilerMarker(type.Name + "." + _name);

                var entry = new Entry { Listener = listener, Order = order, Marker = marker };
                if (_passes > 0)
                    _waiting.Add(entry);
                else
                    Insert(entry);

                Count++;
            }

            public void Remove(T listener)
            {
                for (int i = 0; i < _waiting.Count; i++)
                {
                    if (ReferenceEquals(_waiting[i].Listener, listener))
                    {
                        _waiting.RemoveAt(i);
                        Count--;
                        return;
                    }
                }

                for (int i = 0; i < _entries.Count; i++)
                {
                    if (!ReferenceEquals(_entries[i].Listener, listener))
                        continue;

                    if (_passes > 0)
                    {
                        _entries[i] = default;
                        _hasHoles = true;
                    }
                    else
                    {
                        _entries.RemoveAt(i);
                    }

                    Count--;
                    return;
                }
            }

            public void Clear()
            {
                if (_passes > 0)
                {
                    for (int i = 0; i < _entries.Count; i++)
                        _entries[i] = default;
                    _hasHoles = true;
                }
                else
                {
                    _entries.Clear();
                }

                _waiting.Clear();
                Count = 0;
            }

            public void Tick(float deltaTime, Action<T, float> call)
            {
                _passes++;
                try
                {
                    for (int i = 0; i < _entries.Count; i++)
                    {
                        Entry entry = _entries[i];
                        if (entry.Listener == null)
                            continue;

                        try
                        {
#if ENABLE_PROFILER
                            using (entry.Marker.Auto())
#endif
                                call(entry.Listener, deltaTime);
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception, entry.Listener as Object);
                        }
                    }
                }
                finally
                {
                    EndPass();
                }
            }

            private void EndPass()
            {
                if (--_passes > 0)
                    return;

                if (_hasHoles)
                {
                    _entries.RemoveAll(static entry => entry.Listener == null);
                    _hasHoles = false;
                }

                for (int i = 0; i < _waiting.Count; i++)
                    Insert(_waiting[i]);

                _waiting.Clear();
            }

            // After every entry of the same or lower order: equal orders keep the order they joined in
            private void Insert(Entry entry)
            {
                int index = _entries.Count;
                while (index > 0 && _entries[index - 1].Order > entry.Order)
                    index--;

                _entries.Insert(index, entry);
            }
        }

        /// <summary>Identity, not equality: UnityEngine.Object calls two destroyed objects equal.</summary>
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
