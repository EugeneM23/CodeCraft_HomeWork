using System;
using System.Reflection;
using Atomic.Entities;
using Ticking;
using UnityEngine;

namespace Game.GameLoop.Adapters
{
    /// <summary>
    /// Ticks the Atomic entities from the game loop: Atomic's own TickableManager (internal to the plugin) is switched
    /// off, so Unity no longer calls it, and its Update, FixedUpdate and LateUpdate are called from the loop's phases
    /// instead — a paused loop stands every entity still. Found by reflection once; the calls go through delegates
    /// bound to the manager. The manager is made lazily and made again on every Play: a destroyed one is looked up
    /// again on the next tick. Without the type or its methods (a newer Atomic) the error is logged once and Atomic
    /// keeps ticking itself. Disposed, the manager ticks itself again.
    /// </summary>
    public sealed class AtomicTickBridge : IUpdateListener, IFixedUpdateListener, ILateUpdateListener, IDisposable
    {
        private const string MANAGER_TYPE = "Atomic.Entities.TickableManager";
        private const BindingFlags STATIC = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;

        private static bool s_resolved;
        private static bool s_failed;
        private static Func<MonoBehaviour> s_instance;
        private static MethodInfo s_update;
        private static MethodInfo s_fixedUpdate;
        private static MethodInfo s_lateUpdate;

        private MonoBehaviour _manager;
        private Action _update;
        private Action _fixedUpdate;
        private Action _lateUpdate;
        private bool _disposed;

        /// <summary>The bridge found Atomic's tick manager and drives it.</summary>
        public bool IsConnected => !_disposed && _manager != null;

        public void OnUpdate(float deltaTime)
        {
            if (Connect())
                _update();
        }

        public void OnFixedUpdate(float deltaTime)
        {
            if (Connect())
                _fixedUpdate();
        }

        public void OnLateUpdate(float deltaTime)
        {
            if (Connect())
                _lateUpdate();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            if (_manager != null)
                _manager.enabled = true;

            _manager = null;
            _update = _fixedUpdate = _lateUpdate = null;
        }

        // True with a live manager under the bridge's hand; a new one (a new Play) is switched off and bound
        private bool Connect()
        {
            if (_disposed)
                return false;
            if (_manager != null)
                return true;
            if (!Resolve())
                return false;

            MonoBehaviour manager = s_instance();
            if (manager == null)
                return false;

            manager.enabled = false;
            _manager = manager;
            _update = (Action)Delegate.CreateDelegate(typeof(Action), manager, s_update);
            _fixedUpdate = (Action)Delegate.CreateDelegate(typeof(Action), manager, s_fixedUpdate);
            _lateUpdate = (Action)Delegate.CreateDelegate(typeof(Action), manager, s_lateUpdate);
            return true;
        }

        private static bool Resolve()
        {
            if (s_resolved)
                return !s_failed;

            s_resolved = true;
            try
            {
                Type type = typeof(IEntity).Assembly.GetType(MANAGER_TYPE);
                MethodInfo getter = type?.GetProperty("Instance", STATIC)?.GetGetMethod(true);
                s_update = type?.GetMethod("Update", INSTANCE, null, Type.EmptyTypes, null);
                s_fixedUpdate = type?.GetMethod("FixedUpdate", INSTANCE, null, Type.EmptyTypes, null);
                s_lateUpdate = type?.GetMethod("LateUpdate", INSTANCE, null, Type.EmptyTypes, null);

                if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type) || getter == null || s_update == null
                    || s_fixedUpdate == null || s_lateUpdate == null)
                {
                    s_failed = true;
                    Debug.LogError($"{nameof(AtomicTickBridge)}: {MANAGER_TYPE} with a static Instance and " +
                                   "Update/FixedUpdate/LateUpdate was not found in Atomic.Entities. Atomic entities " +
                                   "tick themselves and do not stop on pause.");
                    return false;
                }

                s_instance = (Func<MonoBehaviour>)Delegate.CreateDelegate(typeof(Func<MonoBehaviour>), getter);
                return true;
            }
            catch (Exception exception)
            {
                s_failed = true;
                Debug.LogException(exception);
                return false;
            }
        }
    }
}
