using System;
using Modules.Planets;
using UnityEngine;

namespace Game.Views
{
    public interface IPlanetPopupViewPresenter
    {
        // Events
        event Action OnShow;
        event Action OnMoneyChanged;
        event Action OnUpgraded;

        // Properties
        string UpgradePrice { get; }
        string Population { get; }
        string Level { get; }
        string Income { get; }
        Sprite Icon { get; }
        bool CanUpgrade { get; }

        // Methods
        void Show(Planet planet);
        void UpgradePlanet();
    }
}