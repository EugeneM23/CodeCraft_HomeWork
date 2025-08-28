using System;

namespace Game
{
    public interface ILockViewPresenter
    {
        event Action OnPlanetUnlocked;
        bool IsPlanetUnlocked { get; }
    }
}