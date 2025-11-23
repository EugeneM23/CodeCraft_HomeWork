using Atomic.Elements;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    public class DeathAnimBehaviour : IEntityInit
    {
        private static int DEATH = Animator.StringToHash("Death");
        private BaseEvent<TakeDamageArgs> _deathTakenEvent;
        private Animator _animator;

        public void Init(in IEntity entity)
        {
            _deathTakenEvent = entity.GetDeathTakenEvent();
            _animator = entity.GetAnimator();

            _deathTakenEvent.Subscribe(OnDeath);
        }

        private void OnDeath(TakeDamageArgs _)
        {
            _animator.Play(DEATH);
        }
    }
}