using Atomic.Entities;

namespace Game
{
    public static class DamageUseCase
    {
        public static void TakeDamage(IEntity target, TakeDamageArgs args)
        {
            IEntity source = args.source;
            target.GetHealth().Reduce(source.GetDamage().Value);
            target.GetDamageTakenEvent().Invoke(args);
        }
    }
}