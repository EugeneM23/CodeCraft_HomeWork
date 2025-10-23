using Game.Scripts.PlayerController.Game.Scripts.PlayerController;

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

        public float HandleJump()
        {
            if (_inputHandler.JumpToConsume)
            {
                _inputHandler.ConsumeJump();
                return _stats.JumpPower;
            }

            return 0;
        }
    }
}