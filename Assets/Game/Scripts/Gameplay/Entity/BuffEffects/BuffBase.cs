using Atomic.Entities;
using Atomic.Extensions;
using UnityEngine;

namespace Game
{
    public abstract class BuffBase : IEntityAspect
    {
        private readonly BuffConfig _config;
        public string Name => _config.Name;
        public Sprite Icon => _config.Icon;

        protected BuffBase(BuffConfig config)
        {
            _config = config;
        }

        public abstract void  Apply(IEntity entity);

        public abstract void Discard(IEntity entity);
    }
}