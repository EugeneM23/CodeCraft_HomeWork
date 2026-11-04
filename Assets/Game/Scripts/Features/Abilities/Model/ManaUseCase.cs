using Atomic.Elements;

namespace Game
{
    public static class ManaUseCase
    {
        public static bool Enough(in IPlayerContext context, in int amount)
        {
            return context.GetCurrentMana().Value >= amount;
        }
        
        public static bool Spend(in IPlayerContext context, int amount)
        {
            IReactiveVariable<int> currentMana = context.GetCurrentMana();
            if (currentMana.Value < amount)
                return false;

            currentMana.Value -= amount;
            return true;
        } 
    }
}