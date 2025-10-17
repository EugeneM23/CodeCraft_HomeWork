using UnityEngine;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public class FallState : IState
    {
        public StateMachine StateMachine { get; }

        public FallState(StateMachine stateMachine)
        {
            StateMachine = stateMachine;
        }

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public void Tick()
        {
        }
    }
}