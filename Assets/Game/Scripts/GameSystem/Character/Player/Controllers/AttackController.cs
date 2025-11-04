using UnityEngine;

namespace Gameplay.Controllers
{
    public class AttackController : ITickable
    {
        [Inject] private readonly Character _character;

        public void Tick()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F)) 
                _character.Attack();
        }
    }
}