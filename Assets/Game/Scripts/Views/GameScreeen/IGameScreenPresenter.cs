using System;

namespace Game.Views
{
    public interface IGameScreenPresenter
    {
        public event Action OnPlanetPopupShow;
        public event Action OnPlanetPopupHide;
        void ShowPlanetPopup();
        void HidePlanetPopup();
    }
}