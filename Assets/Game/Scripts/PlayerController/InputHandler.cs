using UnityEngine;

namespace Game.Scripts.PlayerController.Game.Scripts.PlayerController
{
    public class InputHandler
    {
        public FrameInput FrameInput { get; private set; }
        public bool JumpToConsume { get; private set; }

        public void HandleInput()
        {
            FrameInput = new FrameInput
            {
                Move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
                JumpDown = Input.GetKeyDown(KeyCode.Space),
                JumpHeld = Input.GetKey(KeyCode.Space)
            };

            if (FrameInput.JumpDown) 
                JumpToConsume = true;
        }

        public void ConsumeJump()
        {
            JumpToConsume = false;
        }
    }
}