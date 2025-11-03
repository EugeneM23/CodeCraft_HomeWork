using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class JumpController : ITickable
    {
        [Inject] private readonly CharacterController2D _character;

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                _character.Jump();
        }
    }
}