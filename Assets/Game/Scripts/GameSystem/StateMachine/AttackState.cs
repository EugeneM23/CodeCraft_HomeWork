using Game.Scripts.Modules.SpriteAnimator;
using Gameplay;
using UnityEngine;

namespace Game.Scripts.GameSystem.Controllers.Player.StateMachine
{
    public class AttackState : IState
    {
        [Inject] private readonly Gameplay.Player _player;
        [Inject] private readonly SpriteAnimator _animator;
        private IPushSideComponent _sideComponent => _player;

        public void Enter()
        {
            _animator
                .Play(AnimationID.Attack)
                .Interrupt(false)
                .AddEvent(_sideComponent.Push, 5);
        }
    }
}