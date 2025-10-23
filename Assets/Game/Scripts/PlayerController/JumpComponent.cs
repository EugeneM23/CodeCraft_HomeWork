using Game.Scripts.PlayerController.Game.Scripts.PlayerController;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpComponent
    {
        private ScriptableStats _stats;
        private readonly InputHandler _inputHandler;
        private readonly SurfaceTangentDebugger _debugger;

        public JumpComponent(ScriptableStats stats, InputHandler inputHandler, SurfaceTangentDebugger debugger)
        {
            _stats = stats;
            _inputHandler = inputHandler;
            _debugger = debugger;
        }

        public float HandleJump()
        {
            if (_inputHandler.JumpToConsume)
            {
                _debugger.SurfaceNormal = Vector2.zero;
                _inputHandler.ConsumeJump();
                return _stats.JumpPower;
            }

            return 0;
        }
    }
}