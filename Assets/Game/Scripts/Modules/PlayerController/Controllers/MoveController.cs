using UnityEngine;

namespace Modules.PlayerController
{
    internal class MoveController : ITickable, IMoveController
    {
        private readonly CharacterController2D _character;

        public MoveController(CharacterController2D character) => _character = character;
        public Vector2 CurrentDirection { get; private set; }

        public void Tick()
        {
            CurrentDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _character.Move(CurrentDirection);
        }
    }
}