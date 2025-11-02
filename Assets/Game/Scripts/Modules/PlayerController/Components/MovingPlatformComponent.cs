using UnityEngine;

namespace Modules.PlayerController
{
    internal class MovingPlatformComponent : IVelocity
    {
        private readonly CharacterController2D _character;

        private Transform _currentPlatform;
        private Vector3 _lastPlatformPosition;

        public MovingPlatformComponent(CharacterController2D character) => _character = character;

        public Vector2 GetVelocity()
        {
            if (!_character.IsGrounded)
            {
                _currentPlatform = null;
                return Vector2.zero;
            }

            Vector2 rayOrigin = new Vector2(_character.transform.position.x, _character.transform.position.y - 0.05f);

            RaycastHit2D hit = Physics2D.Raycast(
                rayOrigin,
                Vector2.down,
                _character.Stats.GrounderDistance,
                _character.Stats.LayerMask
            );

            if (hit.collider != null)
            {
                if (_currentPlatform != hit.collider.transform)
                {
                    _currentPlatform = hit.collider.transform;
                    _lastPlatformPosition = _currentPlatform.position;
                    return Vector2.zero;
                }

                Vector3 currentPos = _currentPlatform.position;
                Vector3 delta3 = currentPos - _lastPlatformPosition;
                _lastPlatformPosition = currentPos;


                Vector2 delta = new Vector2(delta3.x, delta3.y) / Time.fixedDeltaTime;

                return delta;
            }

            _currentPlatform = null;
            return Vector2.zero;
        }
    }
}