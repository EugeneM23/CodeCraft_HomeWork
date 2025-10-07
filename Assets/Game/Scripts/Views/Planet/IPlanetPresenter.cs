using System;
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
        string Price { get; }

        void OnClick();
        void OnHold();
        void GatherIncome();
    }
}