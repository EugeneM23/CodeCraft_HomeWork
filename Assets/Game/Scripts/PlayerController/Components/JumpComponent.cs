using Game.Scripts.PlayerController;
using UnityEngine;

public class JumpComponent : ITickable
{
    private readonly PlayerController _player;

    private float _coyoteTime = 0.1f;
    private float _lastGroundedTime;

    private int _availableJumps;
    private bool _jumpPressedThisFrame = false;

    // Jump buffer по расстоянию
    private float _jumpBufferDistance = 6f; // например, 0.2 юнита

    public JumpComponent(PlayerController player)
    {
        _player = player;
        _availableJumps = _player.Stats.MaxJumps;
    }

    public void Tick()
    {
        // Сбрасываем прыжки, если игрок на земле / склоне / лестнице
        if (_player.IsGrounded || _player.IsOnSlope || _player.IsOnStairs)
        {
            _lastGroundedTime = Time.time;
            _availableJumps = _player.Stats.MaxJumps;
        }

        // Сбрасываем флаг нажатия
        _jumpPressedThisFrame = false;
    }

    public void Jump()
    {
        // Wall jump
        if (_player.IsOnWall && !_player.IsGrounded && _player.MoveDirection != Vector2.zero)
        {
            float jumpX = (-_player.WallDirection * _player.Stats.JumpFromWall) * 0.6f;
            float jumpY = _player.Stats.JumpFromWall;
            Vector2 jumpDirection = new Vector2(jumpX * 1.3f, jumpY);
            _player.AddImpulse(jumpDirection);
            return;
        }

        // Slope / stairs jump
        if (_player.IsOnSlope || _player.IsOnStairs)
        {
            Vector2 jumpDirection = (Vector2.up).normalized * _player.Stats.JumpPower;
            _player.AddImpulse(jumpDirection);
            return;
        }

        bool nearGround = _player.DistanceToGround <= _jumpBufferDistance;
        bool canJump = _player.IsGrounded || nearGround || (Time.time - _lastGroundedTime <= _coyoteTime) ||
                       _availableJumps > 0;

        if (canJump)
        {
            _player.AddImpulse(new Vector2(0, _player.Stats.JumpPower));

            // Если не на земле и не “почти на земле”, уменьшаем доступные мульти-прыжки
            if (!_player.IsGrounded && !nearGround && Time.time - _lastGroundedTime > _coyoteTime)
            {
                _availableJumps--;
            }
        }
    }
}