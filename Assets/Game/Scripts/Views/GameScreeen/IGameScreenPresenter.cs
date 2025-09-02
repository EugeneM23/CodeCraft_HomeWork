using System;

namespace Game.Views.GameScreeen
{
    public interface IGameScreenPresenter
    {
        public event Action<bool> OnPlanetPopupVisible;
        bool IsPlanetPopupVisible { get; }
        void ShowPlanetPopup();
        void HidePlanetPopup();
    }
}