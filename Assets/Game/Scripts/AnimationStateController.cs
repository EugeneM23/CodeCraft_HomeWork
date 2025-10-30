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
            if (_player.IsOnWallSliding)
            {
                _animator.Play(AnimationID.WallSlide);
                return;
            }

            
            if (!_player.IsGrounded && !_player.IsOnWallSliding)
            {
                _animator.Play(AnimationID.Fall);
                return;
            }

            if (Mathf.Abs(_player.Velocity.x) < 1f && _player.IsGrounded)
            {
                _animator.Play(AnimationID.Idle);
                return;
            }

            if (Mathf.Abs(_player.Velocity.x) > 1 && _player.IsGrounded || _player.IsOnSlope)
            {
                _animator.Play(AnimationID.Run);
                return;
            }
        }
    }
}