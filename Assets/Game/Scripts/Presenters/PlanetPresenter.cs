using System;
using Game.Views;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game.Presenters
{
    public class PlanetPresenter : IPlanetPresenter, IInitializable, IDisposable
    {
        public event Action<float, string> OnIncomeProgressUpdated;
        public event Action<bool> OnIncomeReady;
        public event Action OnPlanetUnlocked;
        public event Action OnPlanetChanged;

        public bool IsUnlocked => _planet.IsUnlocked;
        public string Price => _planet.Price.ToString();
        public Sprite Icon => _planet.GetIcon(_planet.IsUnlocked);
        public bool IsIncomeReady => _planet.IsIncomeReady;

        private Planet _planet;
        private readonly IGameScreenPresenter _gameScreenPresenter;
        private readonly PlanetPopupPresenter _planetPopupPresenter;

        public PlanetPresenter(
            Planet planet,
            IGameScreenPresenter gameScreenPresenter,
            PlanetPopupPresenter planetPopupPresenter
        )
        {
            _planet = planet;
            _gameScreenPresenter = gameScreenPresenter;
            _planetPopupPresenter = planetPopupPresenter;
        }

        public void Initialize() => Subscribe();

        public void Dispose() => Unsubscribe();

        private void Subscribe()
        {
            _planet.OnUnlocked += HandlePlanetUnlocked;
            _planet.OnIncomeReady += HandleIncomeChanged;
            _planet.OnGathered += HandleIncomeGathered;
            _planet.OnIncomeTimeChanged += HandleIncomeTimeChanged;
        }

        private void Unsubscribe()
        {
            _planet.OnUnlocked -= HandlePlanetUnlocked;
            _planet.OnIncomeReady -= HandleIncomeChanged;
            _planet.OnGathered -= HandleIncomeGathered;
            _planet.OnIncomeTimeChanged -= HandleIncomeTimeChanged;
        }

        public void OnClick()
        {
            _planet.Unlock();
        }

        public void OnHold()
        {
            if (!_planet.IsUnlocked) return;
            _planetPopupPresenter.SetPlanet(_planet);
            _gameScreenPresenter.ShowPlanetPopup();
        }

        public void GatherIncome()
        {
            _planet.GatherIncome();
        }

        public void SetPlanet(Planet planet)
        {
            Unsubscribe();
            _planet = planet;
            Subscribe();

            OnPlanetChanged?.Invoke();
        }

        private void HandlePlanetUnlocked() => OnPlanetUnlocked?.Invoke();

        private void HandleIncomeChanged(bool isReady) => OnIncomeReady?.Invoke(isReady);

        private void HandleIncomeGathered(int amount) => OnIncomeReady?.Invoke(_planet.IsIncomeReady);

        private void HandleIncomeTimeChanged(float timeLeft)
        {
            float progress = _planet.IncomeProgress;
            string timeText = FormatTime(timeLeft);
            OnIncomeProgressUpdated?.Invoke(progress, timeText);
        }

        private string FormatTime(float seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            return $"{timeSpan.Minutes:D2}m : {timeSpan.Seconds:D2}s";
        }
    }
}