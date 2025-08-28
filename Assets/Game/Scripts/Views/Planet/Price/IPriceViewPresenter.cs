using System;

namespace Game.Views
{
    public interface IPriceViewPresenter
    {
        string Price { get; }
        bool IsPlanetUnlocked { get; }
        event Action OnPlanetUnlocked;
    }
}