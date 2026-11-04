using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    public class LifeTimeBehaviour : IEntityTick, IEntityInit
    {
        private CooldownTimer _lifeTime;
        private IAction _destroyAction;

        public void Init(IEntity entity)
        {
            _lifeTime = entity.GetLifeTime();
            _destroyAction = entity.GetDestroyAction();
        }

        public void Tick(IEntity entity, float deltaTime)
        {
            _lifeTime.Tick(deltaTime);

            if (_lifeTime.IsExpired()) 
                _destroyAction.Invoke();
        }
    }
}