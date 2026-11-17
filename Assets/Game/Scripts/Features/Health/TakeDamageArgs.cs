using Atomic.Entities;

namespace Game
{
    public class TakeDamageArgs
    {
        public IEntity source;

        /// <summary>Share of the source's damage this hit deals: 1 for a direct hit, less at the edge of an explosion.</summary>
        public float damageFactor;

        public TakeDamageArgs(IEntity source, float damageFactor = 1f)
        {
            this.source = source;
            this.damageFactor = damageFactor;
        }
    }
}