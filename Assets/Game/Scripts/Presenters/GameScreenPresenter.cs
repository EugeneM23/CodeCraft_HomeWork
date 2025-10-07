using System;
using Game.Views;

namespace Game.Presenters
{
    public class GameScreenPresenter : IGameScreenPresenter
    {
        public event Action OnPlanetPopupShow;
        public event Action OnPlanetPopupHide;

        public void HidePlanetPopup()
        {
            OnPlanetPopupHide?.Invoke();
        }

        public void ShowPlanetPopup()
        {
            OnPlanetPopupShow?.Invoke();
        }
    }
}