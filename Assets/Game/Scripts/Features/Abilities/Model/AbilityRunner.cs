using System.Collections.Generic;
using Atomic.Entities;

namespace Game
{
    public class AbilityRunner :
        IPlayerContextInit,
        IPlayerContextTick,
        IPlayerContextFixedTick,
        IPlayerContextLateTick,
        IPlayerContextEnable,
        IPlayerContextDisable,
        IPlayerContextDispose
    {
        private readonly List<Ability> _ability = new List<Ability>();

        public void Init(IPlayerContext context)
        {
            foreach (var item in context.GetAbilities()) 
                _ability.Add(item.Value);

            foreach (var item in _ability)
                item.Init();
        }

        public void Tick(IPlayerContext context, float deltaTime)
        {
            foreach (var item in _ability)
                item.Tick(deltaTime);
        }

        public void FixedTick(IPlayerContext context, float deltaTime)
        {
            foreach (var item in _ability)
                item.FixedTick(deltaTime);
        }

        public void LateTick(IPlayerContext context, float deltaTime)
        {
            foreach (var item in _ability)
                item.LateTick(deltaTime);
        }

        public void Enable(IPlayerContext context)
        {
            foreach (var item in _ability)
                item.Enable();
        }

        public void Disable(IPlayerContext context)
        {
            foreach (var item in _ability)
                item.Disable();
        }

        public void Dispose(IPlayerContext context)
        {
            foreach (var item in _ability)
                item.Dispose();
        }
    }
}