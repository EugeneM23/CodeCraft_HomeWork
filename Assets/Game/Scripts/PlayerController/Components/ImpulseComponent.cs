using Game.Scripts.PlayerController;
using Gameplay;
using UnityEngine;

public class ImpulseComponent : IVelocity
{
    private readonly PlayerController _player;

    private Vector2 _impulse;
    private bool _verticalImpulseApplied;
    private float _impulseStartTime;
    private const float WALL_JUMP_LOCK_TIME = 0.2f; // Время блокировки контроля после wall jump

    public ImpulseComponent(PlayerController player)
    {
        _player = player;
        _impulse = Vector2.zero;
        _verticalImpulseApplied = false;
        _impulseStartTime = -999f;

        _player.OnHit += Reset;
    }

    public void AddImpulse(Vector2 impulseValue)
    {
        _impulse = impulseValue;
        _verticalImpulseApplied = false;
        _impulseStartTime = Time.time;
    }

    public void Reset()
    {
        _impulse = Vector2.zero;
        _verticalImpulseApplied = false;
        _impulseStartTime = -999f;
    }

    public Vector2 GetVelocity()
    {
        float horizontalComponent = UpdateHorizontalImpulse();
        float verticalComponent = UpdateVerticalImpulse();

        return new Vector2(horizontalComponent, verticalComponent);
    }

    private float UpdateHorizontalImpulse()
    {
        if (_impulse.x == 0)
            return 0;

        float currentInputDirection = _player.MoveDirection.x;
        float horizontalComponent = _impulse.x;
        
        // ИСПРАВЛЕНИЕ: Проверяем, прошло ли достаточно времени после wall jump
        bool isWallJumpLocked = (Time.time - _impulseStartTime) < WALL_JUMP_LOCK_TIME;

        if (currentInputDirection != 0)
        {
            float impulseDirection = Mathf.Sign(_impulse.x);

            if (Mathf.Sign(currentInputDirection) != impulseDirection)
            {
                // Игрок жмёт в противоположную сторону
                
                if (isWallJumpLocked)
                {
                    // ИСПРАВЛЕНИЕ: В первые моменты после wall jump 
                    // позволяем только слабое влияние input'а
                    float weakInputForce = 30; // Слабее чем обычно
                    _impulse.x = Mathf.MoveTowards(_impulse.x, 0, weakInputForce * Time.fixedDeltaTime);
                }
                else
                {
                    // После блокировки - нормальное торможение
                    float inputForce = 100;
                    _impulse.x = Mathf.MoveTowards(_impulse.x, 0, inputForce * Time.fixedDeltaTime);
                }
                
                horizontalComponent = _impulse.x;
            }
            else
            {
                // Игрок жмёт в ту же сторону, что и импульс
                
                if (isWallJumpLocked)
                {
                    // ИСПРАВЛЕНИЕ: Во время блокировки НЕ заменяем импульс на MaxSpeed
                    // Просто оставляем текущий импульс
                    horizontalComponent = _impulse.x;
                }
                else
                {
                    // После блокировки - можно ускоряться
                    float inputX = _player.MoveDirection.x * _player.Stats.MaxSpeed;

                    if (Mathf.Abs(inputX) > Mathf.Abs(_impulse.x))
                        _impulse.x = inputX;
                }
            }
        }
        else
        {
            // Нет инпута — уменьшаем импульс силой противодействия
            float decelerationRate = 50;
            _impulse.x = Mathf.MoveTowards(_impulse.x, 0, decelerationRate * Time.fixedDeltaTime);
            horizontalComponent = _impulse.x;
        }

        return horizontalComponent;
    }

    private float UpdateVerticalImpulse()
    {
        if (_verticalImpulseApplied || _impulse.y == 0)
            return 0;

        _verticalImpulseApplied = true;
        return _impulse.y;
    }
}