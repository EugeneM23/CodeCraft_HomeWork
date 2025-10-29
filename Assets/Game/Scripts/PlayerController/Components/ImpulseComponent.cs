using Game.Scripts.PlayerController;
using UnityEngine;

public class ImpulseComponent : IVelocity
{
    private readonly PlayerController _player;

    private Vector2 _impulse;
    private bool _verticalImpulseApplied;

    public ImpulseComponent(PlayerController player)
    {
        _player = player;
        _impulse = Vector2.zero;
        _verticalImpulseApplied = false;

        _player.OnHit += Reset;
    }

    public void AddImpulse(Vector2 impulseValue)
    {
        _impulse = impulseValue;
        _verticalImpulseApplied = false;
    }

    public void Reset()
    {
        _impulse = Vector2.zero;
        _verticalImpulseApplied = false;
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

        if (currentInputDirection != 0)
        {
            float impulseDirection = Mathf.Sign(_impulse.x);

            if (Mathf.Sign(currentInputDirection) != impulseDirection)
            {
                // Игрок жмёт в противоположную сторону — уменьшаем импульс силой инпута
                float inputForce = 100;
                _impulse.x = Mathf.MoveTowards(_impulse.x, 0, inputForce * Time.fixedDeltaTime);
                horizontalComponent = _impulse.x;
            }
            else
            {
                // Инпут совпадает с направлением импульса — сбрасываем импульс
                //_impulse.x = 0;
                horizontalComponent = 0;
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