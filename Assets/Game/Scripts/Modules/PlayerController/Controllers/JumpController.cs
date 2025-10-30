using Game.Scripts.Modules.PlayerController.Components;
using UnityEngine;

namespace Game.Scripts.Modules.PlayerController.Controllers
{
    internal class JumpController : ITickable
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