using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atomic.Contexts
{
    public sealed class ContextRunner
    {
        public event Action OnInitialized;
        public event Action OnDisposed;
        public event Action OnEnabled;
        public event Action OnDisabled;

        public event Action<float> OnUpdated;
        public event Action<float> OnFixedUpdated;
        public event Action<float> OnLateUpdated;

        private readonly List<IContext> _contexts;
        private readonly List<IContext> _cache = new();

        public bool Initialized => _initialized;
        public bool Enabled => _enabled;

        private bool _initialized;
        private bool _enabled;

        public ContextRunner()
        {
            _contexts = new List<IContext>();
        }

        public ContextRunner(IEnumerable<IContext> entities)
        {
            _contexts = new List<IContext>(entities);
        }

        public void Init()
        {
            if (_initialized)
            {
                Debug.LogWarning("Context Runner is already initialized!");
                return;
            }

            _initialized = true;

            int count = _contexts.Count;
            if (count != 0)
            {
                _cache.Clear();
                _cache.AddRange(_contexts);

                for (int i = 0; i < count; i++)
                    _cache[i].Init();
            }

            this.OnInitialized?.Invoke();
        }

        public void Dispose()
        {
            if (_enabled)
                this.Disable();

            if (!_initialized)
            {
                Debug.LogWarning("Context Runner is already disposed!");
                return;
            }

            _initialized = false;

            int count = _contexts.Count;
            if (count != 0)
            {
                _cache.Clear();
                _cache.AddRange(_contexts);

                for (int i = 0; i < count; i++)
                    _cache[i].Dispose();
            }

            this.OnDisposed?.Invoke();

            //Auto unsubscribe events:
            DelegateUtils.Unsubscribe(ref this.OnInitialized);
            DelegateUtils.Unsubscribe(ref this.OnEnabled);
            DelegateUtils.Unsubscribe(ref this.OnDisabled);
            DelegateUtils.Unsubscribe(ref this.OnUpdated);
            DelegateUtils.Unsubscribe(ref this.OnFixedUpdated);
            DelegateUtils.Unsubscribe(ref this.OnLateUpdated);
            DelegateUtils.Unsubscribe(ref this.OnDisposed);
        }

        public void Enable()
        {
            if (!_initialized)
                this.Init();

            if (_enabled)
            {
                Debug.LogWarning("Context Runner is already enabled!");
                return;
            }

            _enabled = true;

            int count = _contexts.Count;
            if (count != 0)
            {
                _cache.Clear();
                _cache.AddRange(_contexts);

                for (int i = 0; i < count; i++)
                    _cache[i].Enable();
            }

            this.OnEnabled?.Invoke();
        }

        public void Disable()
        {
            if (!_enabled)
            {
                Debug.LogWarning("Context Runner is already disabled!");
                return;
            }

            _enabled = false;

            int count = _contexts.Count;
            if (count != 0)
            {
                _cache.Clear();
                _cache.AddRange(_contexts);

                for (int i = 0; i < count; i++)
                    _cache[i].Disable();
            }

            this.OnDisabled?.Invoke();
        }

        public void OnUpdate(in float deltaTime)
        {
            if (!_enabled)
            {
                Debug.LogWarning("Update failed! Context Runner is not enabled!");
                return;
            }

            int count = _contexts.Count;
            if (count != 0)
            {
                _cache.Clear();
                _cache.AddRange(_contexts);

                for (int i = 0; i < count; i++)
                    _cache[i].OnUpdate(deltaTime);
            }

            this.OnUpdated?.Invoke(deltaTime);
        }

        public void OnFixedUpdate(in float deltaTime)
        {
            if (!_enabled)
            {
                Debug.LogWarning("Fixed update failed! Context Runner is not enabled!");
                return;
            }

            int count = _contexts.Count;
            if (count != 0)
            {
                _cache.Clear();
                _cache.AddRange(_contexts);

                for (int i = 0; i < count; i++)
                    _cache[i].OnFixedUpdate(deltaTime);
            }

            this.OnFixedUpdated?.Invoke(deltaTime);
        }

        public void OnLateUpdate(in float deltaTime)
        {
            if (!_enabled)
            {
                Debug.LogWarning("Late update failed! Context Runner is not enabled!");
                return;
            }

            int count = _contexts.Count;
            if (count != 0)
            {
                _cache.Clear();
                _cache.AddRange(_contexts);

                for (int i = 0; i < count; i++)
                    _cache[i].OnLateUpdate(deltaTime);
            }

            this.OnLateUpdated?.Invoke(deltaTime);
        }

        public bool Add(in IContext context)
        {
            if (context == null)
                return false;

            if (_contexts.Contains(context))
                return false;

            _contexts.Add(context);

            if (_initialized) context.Init();
            if (_enabled) context.Enable();

            return true;
        }

        public bool Del(in IContext context)
        {
            if (context == null)
                return false;

            if (!_contexts.Remove(context))
                return false;

            if (_enabled) context.Disable();
            if (_initialized) context.Dispose();

            return true;
        }

        public void Clear()
        {
            int count = _contexts.Count;
            if (count == 0)
                return;

            if (_enabled)
                for (int i = 0; i < count; i++)
                    _contexts[i].Disable();

            if (_initialized)
                for (int i = 0; i < count; i++)
                    _contexts[i].Dispose();

            _contexts.Clear();
        }
    }
}