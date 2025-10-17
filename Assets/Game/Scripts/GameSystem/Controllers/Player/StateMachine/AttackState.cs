using Game.Scripts.Modules.SpriteAnimator;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public class AttackState : IState
    {
        private readonly StateMachine _stateMachine;
        private readonly SpriteAnimator _animator;

        private float _time;
        private float _animationDuration;

        public AttackState(StateMachine stateMachine, SpriteAnimator animator)
        {
            _stateMachine = stateMachine;
            _animator = animator;
        }

        public void Enter()
        {
            _stateMachine.CanSetState = false;

            int length = _animator.CurrentAnimation.Sprites.Length;
            float speed = _animator.CurrentAnimation.Speed;
            _animationDuration = (length / speed).Log();

            _time = 0;

            "Enter Attack State".Log(Color.red);
        }

        public void Exit()
        {
            "Exit Attack State".Log(Color.green);
        }

        public void Tick()
        {
            _time += Time.deltaTime;
            if (_time >= _animationDuration)
            {
                Debug.Log("tick");
                _stateMachine.CanSetState = true;
            }
        }
    }
}