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
        public event Action<float, string> OnIncomeTimeChanged;
        public event Action OnUnlocked;
        public event Action OnStateChanged;

        public bool IsUnlocked => _planet.IsUnlocked;
        public string Price => _planet.Price.ToString();
        public Sprite Icon => _planet.GetIcon(_planet.IsUnlocked);
        public bool IsIncomeReady => _planet.IsIncomeReady;

        private readonly Planet _planet;
        private readonly IGameScreenPresenter _gameScreenPresenter;
        private readonly IPlanetPopupPresenter _planetPopupPresenter;
        private readonly MoveAnimation _moveAnimation;
        private readonly MoneyWidgetView _widgetView;

        public PlanetPresenter(Planet planet, IGameScreenPresenter gameScreenPresenter,
            IPlanetPopupPresenter planetPopupPresenter, MoveAnimation moveAnimation, MoneyWidgetView widgetView)
        {
            _planet = planet;
            _gameScreenPresenter = gameScreenPresenter;
            _planetPopupPresenter = planetPopupPresenter;
            _moveAnimation = moveAnimation;
            _widgetView = widgetView;
        }

        public void Initialize()
        {
            _planet.OnUnlocked += HandlePlanetUnlocked;
            _planet.OnIncomeReady += HandleIncomeReady;
            _planet.OnGathered += HandleIncomeGathered;
            _planet.OnIncomeTimeChanged += HandleIncomeTimeChanged;
        }

        public void Dispose()
        {
            _planet.OnUnlocked -= HandlePlanetUnlocked;
            _planet.OnIncomeReady -= HandleIncomeReady;
            _planet.OnGathered -= HandleIncomeGathered;
            _planet.OnIncomeTimeChanged -= HandleIncomeTimeChanged;
        }

        public void OnClick() => _planet.Unlock();

        public void OnHold()
        {
            if (!_planet.IsUnlocked) return;

            _planetPopupPresenter.SetPlanet(_planet);
            _gameScreenPresenter.ShowPlanetPopup();
        }

        public void OnCoinClicked(float moveSpeed)
        {
            _moveAnimation.MoveToTarget(
                _widgetView.CoinTarget.position,
                moveSpeed,
                () => _planet.GatherIncome());
        }

        private void HandlePlanetUnlocked()
        {
            OnUnlocked?.Invoke();
            OnStateChanged?.Invoke();
        }

        private void HandleIncomeReady(bool _) => OnStateChanged?.Invoke();

        private void HandleIncomeGathered(int _) => OnStateChanged?.Invoke();

        private void HandleIncomeTimeChanged(float timeLeft)
        {
            float progress = _planet.IncomeProgress;
            string timeText = FormatTime(timeLeft);
            OnIncomeTimeChanged?.Invoke(progress, timeText);
        }

        private string FormatTime(float seconds)
        {
            var timeSpan = TimeSpan.FromSeconds(seconds);
            return $"{timeSpan.Minutes}m : {timeSpan.Seconds}s";
        }
    }
}