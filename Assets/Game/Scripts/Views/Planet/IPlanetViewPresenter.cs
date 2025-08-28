using System;
using Modules.Planets;
using UnityEngine;

namespace Game.Views
{
    public interface IPlanetViewPresenter
    {
        event Action OnUnlocked;

        bool IsUnlocked { get; }
        string Price { get; }
        Sprite Icon { get; }
        void UnlockPlanet();
        void ShowPlanetPopup();
    }
}