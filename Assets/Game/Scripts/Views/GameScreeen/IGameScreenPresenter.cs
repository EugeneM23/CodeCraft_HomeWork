using System;

namespace Game.Views.GameScreeen
{
    public interface IGameScreenPresenter
    {
        public event Action OnPlanetPopupShow;
        public event Action OnPlanetPopupHide;
        void ShowPlanetPopup();
        void HidePlanetPopup();
    }
}