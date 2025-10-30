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
            if (_player.IsOnWallSliding) _animator.Play(AnimationID.WallSlide);
            if (!_player.IsGrounded && !_player.IsOnWallSliding) _animator.Play(AnimationID.Fall);
            if (_player.Velocity.x < 19f && _player.IsGrounded) _animator.Play(AnimationID.Idle);
            if (_player.Velocity.x > 19 && _player.IsGrounded) _animator.Play(AnimationID.Run);
        }
    }
}