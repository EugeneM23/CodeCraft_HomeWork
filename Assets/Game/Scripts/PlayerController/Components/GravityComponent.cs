using Gameplay;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class GravityComponent
    {
        private readonly PlayerController _player;
        private readonly ScriptableStats _stats;
        private readonly CollisionComponent _collision;
        
        private Vector2 _impulse;
        private bool _impulseApplied;

        public GravityComponent(ScriptableStats stats, CollisionComponent collision, PlayerController player)
        {
            _player = player;
            _stats = stats;
            _collision = collision;
            _impulse = Vector2.zero;
            _impulseApplied = false;
            
            // Подписываемся на событие столкновения
            _player.OnHit += ResetImpulse;
        }

        public void AddImpulse(Vector2 impulseValue)
        {
            _player.Restvelocity();
            _impulse = impulseValue;
            _impulseApplied = false;
        }

        private void ResetImpulse()
        {
            _impulse = Vector2.zero;
            _impulseApplied = false;
        }

        public Vector2 GetGravityVector()
        {
            if (_player.IsOnStairs || _player.IsGrabbingLedge)
                return Vector2.zero;

            if (_collision.IsCeilingHit)
                return new Vector2(0, -5);

            float gravity = _stats.FallAcceleration;
            float currentYVelocity = _player.Velocity.y;
            
            // Применяем импульс только один раз
            if (!_impulseApplied && _impulse != Vector2.zero)
            {
                currentYVelocity += _impulse.y;
                _impulseApplied = true;
            }
            
            // Двигаем к максимальной скорости падения
            float newYVelocity = Mathf.MoveTowards(currentYVelocity, -_stats.MaxFallSpeed, gravity * Time.fixedDeltaTime);
            
            // Возвращаем горизонтальный импульс только в первом кадре
            float horizontalComponent = (!_impulseApplied || _impulse.x != 0) ? _impulse.x : 0;
            
            return new Vector2(horizontalComponent, newYVelocity);
        }
    }
}