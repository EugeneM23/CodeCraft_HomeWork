using Game.Scripts.Modules.SpriteAnimator;
using UnityEngine;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public class RunState : IState
    {
        private readonly SpriteAnimator _animator;
        public RunState(SpriteAnimator animator) => _animator = animator;

        public void Enter() => _animator.Play(AnimationID.Run);
    }
}