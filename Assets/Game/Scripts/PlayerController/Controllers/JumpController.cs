using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class JumpController : ITickable
    {
        private readonly PlayerController _player;

        public JumpController(PlayerController player)
        {
            _player = player;
        }

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                _player.Jump();
        }
    }
}