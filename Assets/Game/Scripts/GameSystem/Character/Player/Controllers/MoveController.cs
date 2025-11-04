using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class MoveController : ITickable
    {
        [Inject] private readonly CharacterController2D _character;

        public void Tick()
        {
            Vector2 direction = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _character.SetMoveDirection(direction);
        }
    }
}