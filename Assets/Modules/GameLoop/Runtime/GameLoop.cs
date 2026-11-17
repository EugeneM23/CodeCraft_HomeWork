using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ticking
{
    /// <summary>
    /// The game loop: the owner (GameLoopRunner, a test) calls FixedUpdate, Update and LateUpdate, the loop passes them
    /// on to its listeners — sorted by [DefaultExecutionOrder] of their class, then by the order they came in. While
    /// a source holds a pause nothing ticks and game time stands still. Like Unity: a disabled Behaviour does not tick,
    /// a destroyed one drops out, an exception is logged and the pass goes on. A listener added during a pass ticks
    /// from the next one, a removed one stops at once. A destroyed Unity object that holds a pause lets it go.
    /// </summary>
    public sealed class GameLoop : IGameLoop
    {
        private static readonly Dictionary<Type, int> Orders = new();
        // A profiler sample per listener class: the Profiler shows what every listener costs, not one lump for the loop
        private static readonly Dictionary<Type, ProfilerMarker> Markers = new();

        private readonly ListenerList<IUpdateListener> _updates = new();
        private readonly ListenerList<IFixedUpdateListener> _fixedUpdates = new();
        private readonly ListenerList<ILateUpdateListener> _lateUpdates = new();
        private readonly ListenerList<IPauseListener> _pauseListeners = new();
        private readonly HashSet<object> _listeners = new(ReferenceComparer.Instance);
        private readonly List<object> _pauseSources = new();

        public event Action<bool> PauseChanged;

        public bool IsPaused => _pauseSources.Count > 0;
        public float Time { get; private set; }
        public IReadOnlyList<object> PauseSources => _pauseSources;

        /// <summary>Every listener once, in no particular order (inspector, debugging).</summary>
        public IReadOnlyCollection<object> Listeners => _listeners;
        public int ListenerCount => _listeners.Count;
        public int UpdateListenerCount => _updates.Count;
        public int FixedUpdateListenerCount => _fixedUpdates.Count;
        public int LateUpdateListenerCount => _lateUpdates.Count;
        public int PauseListenerCount => _pauseListeners.Count;

        // ---------- Listeners ----------

        public void Add(object listener)
        {
            if (listener == null)
                throw new ArgumentNullException(nameof(listener));

            if (!(listener is IUpdateListener || listener is IFixedUpdateListener || listener is ILateUpdateListener
                  || listener is IPauseListener))
                throw new ArgumentException(
                    $"{listener.GetType().Name} implements none of the game loop listener interfaces", nameof(listener));

            if (!_listeners.Add(listener))
                return;

            int order = OrderOf(listener.GetType());
            if (listener is IUpdateListener update)
                _updates.Add(update, order);
            if (listener is IFixedUpdateListener fixedUpdate)
                _fixedUpdates.Add(fixedUpdate, order);
            if (listener is ILateUpdateListener lateUpdate)
                _lateUpdates.Add(lateUpdate, order);
            if (listener is IPauseListener pauseListener)
            {
                _pauseListeners.Add(pauseListener, order);
                if (IsPaused)
                    Notify(pauseListener, true);
            }
        }

        public void Remove(object listener)
        {
            if (listener == null || !_listeners.Remove(listener))
                return;

            if (listener is IUpdateListener update)
                _updates.Remove(update);
            if (listener is IFixedUpdateListener fixedUpdate)
                _fixedUpdates.Remove(fixedUpdate);
            if (listener is ILateUpdateListener lateUpdate)
                _lateUpdates.Remove(lateUpdate);
            if (listener is IPauseListener pauseListener)
                _pauseListeners.Remove(pauseListener);
        }

        public bool Contains(object listener) => listener != null && _listeners.Contains(listener);

        // ---------- Pause ----------

        public void Pause(object source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (IndexOfSource(source) >= 0)
                return;

            _pauseSources.Add(source);
            if (_pauseSources.Count == 1)
                ChangePause(true);
        }

        public void Resume(object source)
        {
            int index = IndexOfSource(source);
            if (index < 0)
                return;

            _pauseSources.RemoveAt(index);
            if (_pauseSources.Count == 0)
                ChangePause(false);
        }

        /// <summary>Every source lets go: the owner shuts the loop down, a test cleans up.</summary>
        public void ResumeAll()
        {
            if (_pauseSources.Count == 0)
                return;

            _pauseSources.Clear();
            ChangePause(false);
        }

        public bool IsPausedBy(object source) => IndexOfSource(source) >= 0;

        // ---------- Ticks ----------

        public void FixedUpdate(float deltaTime)
        {
            ReleaseDestroyedSources();
            if (!IsPaused)
                Tick(_fixedUpdates, deltaTime, static (listener, dt) => listener.OnFixedUpdate(dt));
        }

        public void Update(float deltaTime)
        {
            ReleaseDestroyedSources();
            if (IsPaused)
                return;

            Time += deltaTime;
            Tick(_updates, deltaTime, static (listener, dt) => listener.OnUpdate(dt));
        }

        public void LateUpdate(float deltaTime)
        {
            if (!IsPaused)
                Tick(_lateUpdates, deltaTime, static (listener, dt) => listener.OnLateUpdate(dt));
        }

        // ---------- Internal ----------

        private void Tick<T>(ListenerList<T> list, float deltaTime, Action<T, float> call) where T : class
        {
            list.BeginPass();
            try
            {
                for (int i = 0; i < list.SlotCount; i++)
                {
                    T listener = list[i];
                    if (listener == null || !IsAlive(listener) || !IsActive(listener))
                        continue;

                    try
                    {
#if ENABLE_PROFILER
                        using (MarkerOf(listener).Auto())
#endif
                            call(listener, deltaTime);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, listener as Object);
                    }
                }
            }
            finally
            {
                list.EndPass();
            }
        }

        private void ChangePause(bool paused)
        {
            _pauseListeners.BeginPass();
            try
            {
                for (int i = 0; i < _pauseListeners.SlotCount; i++)
                {
                    // A listener resumed (or paused) the game from inside this news: the newer news has gone out
                    if (IsPaused != paused)
                        return;

                    IPauseListener listener = _pauseListeners[i];
                    if (listener != null && IsAlive(listener))
                        Notify(listener, paused);
                }
            }
            finally
            {
                _pauseListeners.EndPass();
            }

            if (IsPaused != paused)
                return;

            try
            {
                PauseChanged?.Invoke(paused);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void Notify(IPauseListener listener, bool paused)
        {
            try
            {
                if (paused)
                    listener.OnPause();
                else
                    listener.OnResume();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, listener as Object);
            }
        }

        // A destroyed Unity object that never left the loop: drop it instead of calling into it
        private bool IsAlive(object listener)
        {
            if (!(listener is Object unityObject) || unityObject != null)
                return true;

            Remove(listener);
            return false;
        }

        private static bool IsActive(object listener) => !(listener is Behaviour behaviour) || behaviour.isActiveAndEnabled;

        private void ReleaseDestroyedSources()
        {
            bool released = false;
            for (int i = _pauseSources.Count - 1; i >= 0; i--)
            {
                if (_pauseSources[i] is Object unityObject && unityObject == null)
                {
                    _pauseSources.RemoveAt(i);
                    released = true;
                }
            }

            if (released && _pauseSources.Count == 0)
                ChangePause(false);
        }

        private int IndexOfSource(object source)
        {
            for (int i = 0; i < _pauseSources.Count; i++)
            {
                if (ReferenceEquals(_pauseSources[i], source))
                    return i;
            }

            return -1;
        }

        private static int OrderOf(Type type)
        {
            if (Orders.TryGetValue(type, out int order))
                return order;

            var attribute = (DefaultExecutionOrder)Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder), true);
            order = attribute?.order ?? 0;
            Orders[type] = order;
            return order;
        }

        private static ProfilerMarker MarkerOf(object listener)
        {
            Type type = listener.GetType();
            if (!Markers.TryGetValue(type, out ProfilerMarker marker))
                Markers[type] = marker = new ProfilerMarker(type.Name);
            return marker;
        }
    }
}
