using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent
    {
        private ScriptableStats _stats;
        private readonly InputHandler _inputHandler;

        public JumpComponent(ScriptableStats stats, InputHandler inputHandler)
        {
            _stats = stats;
            _inputHandler = inputHandler;
        }

        public Vector2 GetJumpVector()
        {
            if (_inputHandler.JumpToConsume)
            {
                _inputHandler.ConsumeJump();
                return new Vector2(0, _stats.JumpPower);
            }

            return Vector2.zero;
        }

    }
}