using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent
    {
        private readonly ScriptableStats _stats;
        private readonly InputHandler _inputHandler;
        private readonly PlayerController _player;

        public JumpComponent(ScriptableStats stats, InputHandler inputHandler, PlayerController player)
        {
            _stats = stats;
            _inputHandler = inputHandler;
            _player = player;
        }

        public Vector2 GetJumpVector()
        {
            if (_inputHandler.JumpToConsume)
            {
                _player.IsOnStairs = false;
                _inputHandler.ConsumeJump();

                if (_player.IsOnStairs)
                    return Vector2.up * _stats.JumpPower;

                return _player.SurfaceNormal * _stats.JumpPower;
            }

            return Vector2.zero;
        }
    }
}