using Atomic.Contexts;
using Atomic.Entities;

namespace Game
{
    public class AbilityRunner : 
        IContextInit<IPlayerContext>,
        IContextUpdate,
        IContextFixedUpdate,
        IContextLateUpdate,
        IContextEnable,
        IContextDisable,
        IContextDispose
    {
        private Ability _ability;
        public void Init(IPlayerContext context)
        {
            _ability = context.GetDashAbility();
            _ability.Init(); 
        }

        public void OnUpdate(IContext context, float deltaTime)
        {
            _ability.OnUpdate(deltaTime);
        }

        public void OnFixedUpdate(IContext context, float deltaTime)
        {
            _ability.OnFixedUpdate(deltaTime);
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            _ability.OnLateUpdate(deltaTime);
        }

        public void Enable(IContext context)
        {
            _ability.Enable();
        }

        public void Disable(IContext context)
        {
            _ability.Disable();
        }

        public void Dispose(IContext context)
        {
            _ability.Dispose(); 
        }
    }
}