using UnityEngine;

namespace Modules.PlayerController
{
    internal class JumpController : ITickable
    {
        private readonly CharacterController2D _character;

        public JumpController(CharacterController2D character)
        {
            _character = character;
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                _character.Jump();
        }
    }
}