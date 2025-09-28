using System;
using UnityEngine;

namespace Game.Views
{
    public interface IPlanetPresenter
    {
        event Action<float, string> OnIncomeTimeChanged;
        event Action OnUnlocked;
        event Action OnStateChanged;

        bool IsUnlocked { get; }
        Sprite Icon { get; }
        bool IsIncomeReady { get; }

        void OnClick();
        void OnHold();
        void OnCoinClicked(float moveSpeed);
    }
}