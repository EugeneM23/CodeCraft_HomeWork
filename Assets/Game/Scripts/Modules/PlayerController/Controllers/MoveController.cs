using UnityEngine;

namespace Modules.PlayerController
{
    internal class MoveController : ITickable
    {
        private readonly PlayerController _player;

        public MoveController(PlayerController player) => _player = player;
        public Vector2 CurrentDirection { get; private set; }

        public void Tick()
        {
            CurrentDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _player.Move(CurrentDirection);
        }
    }
}