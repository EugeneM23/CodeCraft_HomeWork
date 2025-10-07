using System;
using UnityEngine;

namespace Game.Views
{
    public interface IPlanetPopupPresenter
    {
        event Action OnMoneyChanged;
        event Action OnUpgraded;

        string UpgradePrice { get; }
        string Population { get; }
        string Level { get; }
        string Income { get; }
        Sprite Icon { get; }
        bool CanUpgrade { get; }

        void OnCloseClicked();
        void OnUpgradeClicked();
    }
}