using System;
using UnityEngine;

namespace Game.Views
{
    public interface IPlanetPresenter
    {
        event Action OnUnlocked;

        bool IsUnlocked { get; }
        Sprite Icon { get; }
        void OnClick();
        void OnHold();
        void OnCloseClicked();
    }
}