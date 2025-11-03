using Modules.PlayerController;
using UnityEngine;

namespace Gameplay.Controllers
{
    public class AttackController : ITickable
    {
        [Inject] private readonly StateMachine _stateMachine;
        [Inject] private readonly CharacterController2D _character;

        public void Tick()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F))
            {
                _stateMachine.SetState<AttackState>();
            }
        }
    }
}