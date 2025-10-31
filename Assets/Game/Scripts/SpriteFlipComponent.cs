using System;
using Game.Scripts.Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts
{
    public class SpriteFlipComponent : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private PlayerController _player;

        private void Update()
        {
            if (_player.IsOnWallSliding && _player.WallDirection < 0)
            {
                _renderer.flipX = true;
                return;
            }

            if (_player.Velocity.x < 0) _renderer.flipX = true;
            if (_player.Velocity.x > 0) _renderer.flipX = false;
        }
    }
}