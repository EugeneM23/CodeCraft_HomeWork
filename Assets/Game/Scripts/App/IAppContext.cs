using Atomic.Entities;

namespace Game
{
    /// <summary>
    /// The app context: one entity for the whole run of the game, above the level's GameContext. Its values are the
    /// app services (AppContextAPI and the teams' own *API files keyed on IAppContext).
    /// </summary>
    public interface IAppContext : IEntity
    {
    }
}
