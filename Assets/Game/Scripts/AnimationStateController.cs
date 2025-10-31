using System;
using Game.Scripts.Modules.PlayerController;
using Gameplay;
using UnityEngine;

namespace Game.Scripts
{
    public class AnimationStateController : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;
        [SerializeField] private SpriteAnimator _animator;

        private void Update()
        {
            if (_player.IsWallSliding)
            {
                _animator.Play(AnimationID.WallSlide);
                return;
            }

            if (!_player.IsGrounded && !_player.IsWallSliding)
            {
                _animator.Play(AnimationID.Fall);
                return;
            }

            if (Mathf.Abs(_player.MoveDirection.x) > 0.5f && (_player.IsGrounded || _player.IsOnSlope))
            {
                Debug.Log(_player.MoveDirection.x);
                _animator.Play(AnimationID.Run);
                return;
            }

            if (Mathf.Abs(_player.MoveDirection.x) < 0.5f && _player.IsGrounded || _player.IsOnSlope)
            {
                _animator.Play(AnimationID.Idle);
                return;
            }
        }
    }
}