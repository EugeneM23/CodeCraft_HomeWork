using UnityEngine;

namespace Gameplay.Controllers
{
    public class AttackState : BaseState
    {
        private float _animationTime;
        private float _timer;

        public override void Enter()
        {
            _timer = 0;
            AnimationID id = GetRandomAnimation();
            _animator.PlayForce(id).CanBreak(false);
            
            int length = _animator.CurrentAnimation.Sprites.Length;
            _animationTime = length / _animator.CurrentAnimation.FPS;
        }

        private AnimationID GetRandomAnimation()
        {
            int rand = Random.Range(0, 7);

            switch (rand)
            {
                case 0: return AnimationID.KickA;
                case 1: return AnimationID.KickB;
                case 2: return AnimationID.KickC;
                case 3: return AnimationID.PunchA;
                case 4: return AnimationID.PunchB;
                case 5: return AnimationID.PunchC;
                case 6: return AnimationID.PunchA;
            }

            return AnimationID.KickA;
        }

        public override void Tick()
        {
            _timer += Time.deltaTime;
            if (_timer >= _animationTime)
            {
                AnimationFsm.SetState<IdleState>();
            }
        }
    }
}