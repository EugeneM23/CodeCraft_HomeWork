using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameObject.Player
{
    public class ImpulseComponent : ITickable
    {
        private readonly Rigidbody2D _rigidbody;

        private bool _isActive;
        private float _timer;
        private float _duration;

        public ImpulseComponent(Rigidbody2D rigidbody)
        {
            _rigidbody = rigidbody;
        }

        public void AddForce(Vector2 direction, float force, float duration = 0.2f)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.AddForce(direction * force, ForceMode2D.Impulse);

            _duration = duration;
            _timer = duration;
            _isActive = true;
        }

        public void Tick()
        {
            if (!_isActive)
                return;

            _timer -= Time.deltaTime;

            if (_timer <= 0f)
            {
                _isActive = false;
                _timer = 0f;
            }
        }

        public bool OnImpulse() => _isActive;
    }
}