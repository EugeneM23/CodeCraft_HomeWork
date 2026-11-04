using Atomic.Entities;

namespace Game
{
    public static class PlayerUseCase
    {
        public static IPlayerContext GetPlayerContext(GameContext gameContext, IEntity character)
        {
            return gameContext.GetPlayerContext();
        }
    }
}