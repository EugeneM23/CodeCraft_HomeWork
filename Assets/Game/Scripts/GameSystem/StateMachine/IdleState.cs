using Game.Scripts.Modules.SpriteAnimator;
using UnityEngine;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public class IdleState : IState
    {
        private readonly SpriteAnimator _animator;
        public IdleState(SpriteAnimator animator) => _animator = animator;

        public void Enter() => _animator.Play(AnimationID.Idle);
    }
}