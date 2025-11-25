using Atomic.Entities;

namespace Game
{
    public class TakeDamageArgs
    {
        public IEntity source;

        public TakeDamageArgs(IEntity source)
        {
            this.source = source;
        }
    }
}