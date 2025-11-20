using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;

namespace Game.Gameplay
{
    public class MeleeAttackBehaviour : IEntityInit
    {
        private IEntity _character;
        private IReactiveVariable<IEntity> _target;
        private AnimationEventReceiver _animationEventReceiver;

        public void Init(in IEntity entity)
        {
            _character = entity;
            _target = entity.GetTarget();
            _animationEventReceiver = entity.GetAnimationEventReceiver();
            _animationEventReceiver.OnEvent += Invoke;
        }

        public void Invoke(string eventName)
        {
            if (_target.Value == null) return;

            if (eventName == "fire_event")
                _target.Value.GetHealth().Reduce(_character.GetDamage().Value);
        }
    }
}