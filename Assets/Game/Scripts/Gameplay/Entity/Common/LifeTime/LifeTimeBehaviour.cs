using Atomic.Elements;
using Atomic.Entities;
using Modules.Gameplay;

namespace Game
{
    public class LifeTimeBehaviour : IEntityUpdate, IEntityInit
    {
        private Cooldown _lifeTime;
        private IAction _destroyAction;

        public void Init(in IEntity entity)
        {
            _lifeTime = entity.GetLifeTime();
            _destroyAction = entity.GetDestroyAction();
        }

        public void OnUpdate(in IEntity entity, in float deltaTime)
        {
            _lifeTime.Tick(deltaTime);

            if (_lifeTime.IsExpired()) 
                _destroyAction.Invoke();
        }
    }
}