using UnityEngine;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public class RunState : IState
    {
        public StateMachine StateMachine { get; }

        public RunState(StateMachine stateMachine)
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