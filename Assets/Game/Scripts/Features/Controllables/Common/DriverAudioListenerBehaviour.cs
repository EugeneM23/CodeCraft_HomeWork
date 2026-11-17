using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Hears for the player while they sit in this vehicle. The scene's AudioListener is on the
    /// character, and getting in switches the character's GameObject off
    /// (ControllableUseCase.Enter): with nobody listening, every sound went silent from the moment
    /// the player got in — the helicopter's gun among them.
    ///
    /// The vehicle's own listener — found on its root, or added there — is on only while somebody is
    /// inside, so on foot the character's is the one listener in the scene.
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
