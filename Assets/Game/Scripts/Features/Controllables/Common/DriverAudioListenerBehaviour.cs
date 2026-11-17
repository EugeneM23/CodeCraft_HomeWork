using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Hears for the player from the vehicle they drive. The vehicle's own listener — found on its
    /// root, or added there — is on only while it has a Driver, so of several vehicles in a scene
    /// only the player's listens.
    /// </summary>
    public sealed class DriverAudioListenerBehaviour : IEntityInit, IEntityTick
    {
        private IValue<IEntity> _driver;
        private AudioListener _listener;

        public void Init(IEntity entity)
        {
            _driver = entity.GetDriver();

            GameObject root = entity.GetGameObject();
            if (!root.TryGetComponent(out _listener))
                _listener = root.AddComponent<AudioListener>();

            _listener.enabled = _driver.Value != null;
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            bool occupied = _driver.Value != null;
            if (_listener != null && _listener.enabled != occupied)
                _listener.enabled = occupied;
        }
    }
}
