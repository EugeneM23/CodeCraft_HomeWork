using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent
    {
        private readonly ScriptableStats _stats;
        private readonly InputHandler _inputHandler;
        private readonly PlayerController _playerController;

        public JumpComponent(ScriptableStats stats, InputHandler inputHandler, PlayerController playerController)
        {
            _stats = stats;
            _inputHandler = inputHandler;
            _playerController = playerController;
        }

        public Vector2 GetJumpVector()
        {
            if (_inputHandler.JumpToConsume)
            {
                _playerController.IsOnStairs = false;
                _inputHandler.ConsumeJump();
                return new Vector2(0, _stats.JumpPower);
            }

            return Vector2.zero;
        }

    }
}