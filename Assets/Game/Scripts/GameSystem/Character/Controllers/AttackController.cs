using UnityEngine;

namespace Gameplay.Controllers
{
    public class AttackController : ITickable
    {
        [Inject] private readonly AttackComponent _attackComponent;

        public void Tick()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F)) 
                _attackComponent.Attack();
        }
    }
}