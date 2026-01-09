using Atomic.Entities;
using Atomic.Extensions;
using UnityEngine;

namespace Game
{
    public abstract class BaseBuff : ScriptableObject, IEntityAspect
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }

        public abstract void Apply(IEntity entity);
        public abstract void Discard(IEntity entity);
    }
}