using System;
using Modules.Planets;

namespace Game
{
    public class LockViewPresenter : ILockViewPresenter
    {
        public event Action OnPlanetUnlocked;

        private readonly Planet _planet;
        public bool IsPlanetUnlocked => _planet.IsUnlocked;

        public LockViewPresenter(Planet planet) => _planet = planet;

        public void Initialize() => _planet.OnUnlocked += OnPlanetUnlock;

        public void Dispose() => _planet.OnUnlocked -= OnPlanetUnlock;

        private void OnPlanetUnlock() => OnPlanetUnlocked?.Invoke();
    }
}