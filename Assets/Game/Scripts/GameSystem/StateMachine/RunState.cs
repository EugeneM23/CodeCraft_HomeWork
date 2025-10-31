
using UnityEngine;

namespace Gameplay
{
    public class RunState : IState
    {
        private readonly SpriteAnimator _animator;
        public RunState(SpriteAnimator animator) => _animator = animator;

        public void Enter()
        {
            Debug.Log("Run State");
            _animator.Play(AnimationID.Run);
        }
    }
}