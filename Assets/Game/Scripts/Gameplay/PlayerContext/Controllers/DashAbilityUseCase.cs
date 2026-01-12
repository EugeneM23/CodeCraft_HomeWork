using Atomic.Entities;

namespace Game
{
    public static class DashAbilityUseCase
    {
        public static void Dash(IEntity character, IEntity ability)
        {
            ability.GetBaseEvent().Invoke();
        }
    }
}