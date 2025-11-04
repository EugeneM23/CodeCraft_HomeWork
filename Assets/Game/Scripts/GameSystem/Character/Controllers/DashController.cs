using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class DashController : ITickable
    {
        [Inject] private readonly CharacterController2D _character;

        public void Tick()
        {
            bool right = Input.GetKey(KeyCode.D) && Input.GetKeyDown(KeyCode.LeftShift);
            bool left = Input.GetKey(KeyCode.A) && Input.GetKeyDown(KeyCode.LeftShift);

            if (right || left)
            {
                if (right)
                    _character.Dash(Vector2.right * 50f);
                else if (left)
                    _character.Dash(Vector2.left * 50f);
            }
        }
    }
}