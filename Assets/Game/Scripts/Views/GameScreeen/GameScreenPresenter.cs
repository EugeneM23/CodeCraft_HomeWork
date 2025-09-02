using System;

namespace Game.Views.GameScreeen
{
    public class GameScreenPresenter : IGameScreenPresenter
    {
        public event Action<bool> OnPlanetPopupVisible;
        public bool IsPlanetPopupVisible { get; set; }

        public void HidePlanetPopup()
        {
            IsPlanetPopupVisible = false;
            OnPlanetPopupVisible?.Invoke(false);
        }

        public void ShowPlanetPopup()
        {
            IsPlanetPopupVisible = true;
            OnPlanetPopupVisible?.Invoke(true  );
        }
    }
}