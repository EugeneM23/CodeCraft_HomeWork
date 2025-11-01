using UnityEngine;

namespace Modules.PlayerController
{
    internal class ImpulseComponent : IVelocity
    {
        private readonly PlayerController _player;
        private Vector2 _impulse;
        private bool _verticalApplied;

        public ImpulseComponent(PlayerController player)
        {
            _player = player;
            _player.OnCollisionHit += Reset;
        }

        ~ImpulseComponent() => _player.OnCollisionHit -= Reset;

        public void AddImpulse(Vector2 value)
        {
            _impulse = value;
            _verticalApplied = false;
        }

        private void Reset()
        {
            _impulse = new Vector2(0, _player.Velocity.y);
            _verticalApplied = false;
        }

        public Vector2 GetVelocity()
        {
            float x = UpdateHorizontal();
            float y = UpdateVertical();
            return new Vector2(x, y);
        }

        private float UpdateHorizontal()
        {
            _impulse.x = Mathf.MoveTowards(_impulse.x, 0, 100 * Time.fixedDeltaTime);
            return _impulse.x;
        }

        private float UpdateVertical()
        {
            if (_verticalApplied)
                return 0;

            _verticalApplied = true;
            return _impulse.y;
        }
    }
}