using UnityEngine;

namespace Gameplay.Controllers
{
    public class JumpController : ITickable
    {
        [Inject] private readonly JumpComponent _jumpComponent;

        public void Tick()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                _jumpComponent.Jump();
        }
    }
}