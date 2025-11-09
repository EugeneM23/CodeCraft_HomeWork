using System;

namespace Atomic.Entities
{
    public partial class EntityWorld
    {
        public event Action OnInitialized
        {
            add => _entityRunner.OnInitialized += value;
            remove => _entityRunner.OnInitialized -= value;
        }

        public event Action OnDisposed
        {
            add => _entityRunner.OnDisposed += value;
            remove => _entityRunner.OnDisposed -= value;
        }

        public event Action OnEnabled
        {
            add => _entityRunner.OnEnabled += value;
            remove => _entityRunner.OnEnabled -= value;
        }

        public event Action OnDisabled
        {
            add => _entityRunner.OnDisabled += value;
            remove => _entityRunner.OnDisabled -= value;
        }

        public event Action<float> OnUpdated
        {
            add => _entityRunner.OnUpdated += value;
            remove => _entityRunner.OnUpdated -= value;
        }

        public event Action<float> OnFixedUpdated
        {
            add => _entityRunner.OnFixedUpdated += value;
            remove => _entityRunner.OnFixedUpdated -= value;
        }

        public event Action<float> OnLateUpdated
        {
            add => _entityRunner.OnLateUpdated += value;
            remove => _entityRunner.OnLateUpdated -= value;
        }

        public bool Initialized => _entityRunner.Initialized;
        public bool Enabled => _entityRunner.Enabled;

        private readonly EntityRunner _entityRunner = new();
        
        public void Init() => _entityRunner.Init();
        public void Enable() => _entityRunner.Enable();
        public void Disable() => _entityRunner.Disable();
        public void Dispose() => _entityRunner.Dispose();

        public void OnUpdate(float deltaTime) => _entityRunner.OnUpdate(deltaTime);
        public void OnFixedUpdate(float deltaTime) => _entityRunner.OnFixedUpdate(deltaTime);
        public void OnLateUpdate(float deltaTime) => _entityRunner.OnLateUpdate(deltaTime);
    }
}