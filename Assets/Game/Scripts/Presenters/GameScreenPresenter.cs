using System;
using UnityEngine;

namespace Game.Views.GameScreeen
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