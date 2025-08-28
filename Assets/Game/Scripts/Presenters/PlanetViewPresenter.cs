using System;
using Game.Views;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PlanetViewPresenter : IPlanetViewPresenter, IInitializable, IDisposable
    {
        public event Action OnUnlocked;
        public bool IsUnlocked => _planet.IsUnlocked;
        public string Price => _planet.Price.ToString();
        public Sprite Icon => _planet.GetIcon(_planet.IsUnlocked);

        private readonly Planet _planet;
        private readonly PlanetPopupShower _popupShower;

        public PlanetViewPresenter(Planet planet, PlanetPopupShower popupShower)
        {
            _planet = planet;
            _popupShower = popupShower;
        }

        public void Initialize() => _planet.OnUnlocked += OnPlanetUnlocked;

        public void Dispose() => _planet.OnUnlocked -= OnPlanetUnlocked;

        private void OnPlanetUnlocked() => OnUnlocked?.Invoke();

        public void UnlockPlanet()
        {
            _planet.Unlock();
        }

        public void ShowPlanetPopup()
        {
            if (IsUnlocked)
                _popupShower.ShowPopup(_planet);
        }
    }
}