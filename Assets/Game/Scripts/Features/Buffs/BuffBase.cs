using Atomic.Entities;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Base class for buffs.
    /// </summary>
    /// <remarks>
    /// It used to declare IEntityAspect, whose Apply returns void. Nothing in the project ever
    /// consumed a buff through that interface — buffs are created by their configs and stored in
    /// the entity BuffsEffects list — and the contract was wrong anyway: a buff is allowed to
    /// refuse to apply, which an aspect cannot express. BuffUseCase needs that answer to avoid
    /// recording a buff that never attached.
    /// </remarks>
    public abstract class BuffBase
    {
        private readonly BuffConfig _config;
        public string Name => _config.Name;
        public Sprite Icon => _config.Icon;

        protected BuffBase(BuffConfig config)
        {
            _config = config;
        }

        /// <summary>Applies the buff. False means it refused, and nothing was changed.</summary>
        public abstract bool Apply(IEntity entity);

        public abstract void Discard(IEntity entity);
    }
}
