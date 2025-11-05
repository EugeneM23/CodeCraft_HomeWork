using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class DashController : ITickable
    {
        [Inject] private readonly DashComponent _component;

        public void Tick()
        {
            bool right = Input.GetKey(KeyCode.D) && Input.GetKeyDown(KeyCode.LeftShift);
            bool left = Input.GetKey(KeyCode.A) && Input.GetKeyDown(KeyCode.LeftShift);

            if (right || left)
            {
                if (right)
                    _component.Dash(Vector2.right);
                else if (left)
                    _component.Dash(Vector2.left);
            }
        }
    }
}