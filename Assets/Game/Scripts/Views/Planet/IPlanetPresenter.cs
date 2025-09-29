using System;
using Modules.Planets;
using UnityEngine;

namespace Game.Views
{
    public interface IPlanetPresenter
    {
        event Action<float, string> OnIncomeProgressUpdated;
        event Action<bool> OnIncomeReady;
        event Action OnPlanetUnlocked;
        event Action OnPlanetChanged;

        bool IsUnlocked { get; }
        Sprite Icon { get; }
        bool IsIncomeReady { get; }
        Planet Planet { get; }
        string Price { get; }

        void OnClick();
        void OnHold();
        void SetPlanet(Planet item);
    }
}