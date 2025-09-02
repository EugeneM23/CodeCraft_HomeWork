using System;
using Game.Views;
using Game.Views.GameScreeen;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PlanetPresenter : IPlanetPresenter, IInitializable, IDisposable
    {
        public event Action OnUnlocked;
        public bool IsUnlocked => _planet.IsUnlocked;
        public string Price => _planet.Price.ToString();
        public Sprite Icon => _planet.GetIcon(_planet.IsUnlocked);

        private readonly Planet _planet;

        private readonly IGameScreenPresenter _gameScreenPresenter;
        private readonly IPlanetPopupPresenter _planetPopupPresenter;

        public PlanetPresenter(Planet planet, IGameScreenPresenter gameScreenPresenter,
            IPlanetPopupPresenter planetPopupPresenter)
        {
            _planet = planet;
            _gameScreenPresenter = gameScreenPresenter;
            _planetPopupPresenter = planetPopupPresenter;
        }

        public void Initialize() => _planet.OnUnlocked += OnPlanetUnlocked;

        public void Dispose() => _planet.OnUnlocked -= OnPlanetUnlocked;

        public void OnClick() => _planet.Unlock();

        public void OnHold()
        {
            Debug.Log($"OnHold: {_planet}");
            _planetPopupPresenter.SetPlanet(_planet);
            _gameScreenPresenter.ShowPlanetPopup();
        }

        public void OnCloseClicked() => _gameScreenPresenter.HidePlanetPopup();

        private void OnPlanetUnlocked() => OnUnlocked?.Invoke();
    }
}