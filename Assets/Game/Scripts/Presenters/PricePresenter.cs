using System;
using Modules.Planets;
using Zenject;

namespace Game.Views
{
    public class PricePresenter : IInitializable, IDisposable, IPriceViewPresenter
    {
        public event Action OnPlanetUnlocked;

        private readonly Planet _planet;
        public bool IsPlanetUnlocked => _planet.IsUnlocked;

        public string Price => _planet.Price.ToString();

        public PricePresenter(Planet planet) => _planet = planet;

        public void Initialize() => _planet.OnUnlocked += OnPlanetUnlock;

        public void Dispose() => _planet.OnUnlocked -= OnPlanetUnlock;

        private void OnPlanetUnlock() => OnPlanetUnlocked?.Invoke();
    }
}