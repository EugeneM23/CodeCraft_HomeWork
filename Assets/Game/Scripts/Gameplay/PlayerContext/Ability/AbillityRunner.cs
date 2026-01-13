using System.Collections.Generic;
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
        private readonly List<Ability> _ability = new List<Ability>();

        public void Init(IPlayerContext context)
        {
            _ability.Add(context.GetDashAbility());
            _ability.Add(context.GetTeleportAbility());

            foreach (var item in _ability)
                item.Init();
        }

        public void OnUpdate(IContext context, float deltaTime)
        {
            foreach (var item in _ability)
                item.OnUpdate(deltaTime);
        }

        public void OnFixedUpdate(IContext context, float deltaTime)
        {
            foreach (var item in _ability)
                item.OnFixedUpdate(deltaTime);
        }

        public void OnLateUpdate(IContext context, float deltaTime)
        {
            foreach (var item in _ability)
                item.OnLateUpdate(deltaTime);
        }

        public void Enable(IContext context)
        {
            foreach (var item in _ability)
                item.Enable();
        }

        public void Disable(IContext context)
        {
            foreach (var item in _ability)
                item.Disable();
        }

        public void Dispose(IContext context)
        {
            foreach (var item in _ability)
                item.Dispose();
        }
    }
}