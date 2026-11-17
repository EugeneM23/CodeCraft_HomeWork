using Atomic.Elements;
using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// Sends the entity back once its LifeTime runs out.
    /// </summary>
    /// <remarks>
    /// The life starts over every time the entity is enabled: on its first start and every time
    /// the pool rents it out again (EntityPrefabPool activates the object on rent). A LifeTime
    /// timer is created expired, and every caller that rented an effect, a bullet or a casing used
    /// to have to reset it by hand — ten copies — or the object went straight back on its first tick.
    /// </remarks>
    public class LifeTimeBehaviour : IEntityInit, IEntityEnable, IEntityTick
    {
        private CooldownTimer _lifeTime;
        private IAction _destroyAction;

        public void Init(IEntity entity)
        {
            _lifeTime = entity.GetLifeTime();
            _destroyAction = entity.GetDestroyAction();
        }

        public void Enable(IEntity entity) => _lifeTime.Reset();

        public void Tick(IEntity entity, float deltaTime)
        {
            _lifeTime.Tick(deltaTime);

            if (_lifeTime.IsExpired())
                _destroyAction.Invoke();
        }
    }
}
