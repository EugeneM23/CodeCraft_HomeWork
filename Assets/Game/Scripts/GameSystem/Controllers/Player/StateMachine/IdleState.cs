using UnityEngine;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public class IdleState : IState
    {
        public StateMachine StateMachine { get; }

        public IdleState(StateMachine stateMachine)
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