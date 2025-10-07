using System;
using Game.Views;
using Modules.Money;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game.Presenters
{
    public class PlanetPopupPresenter : IPlanetPopupPresenter, IInitializable, IDisposable
    {
        public event Action OnMoneyChanged;
        public event Action OnUpgraded;

        public string Population => _currentPlanet?.Population.ToString() ?? "0";
        public string Level => _currentPlanet?.Level.ToString() ?? "0";
        public string Income => _currentPlanet?.MinuteIncome.ToString() ?? "0";
        public Sprite Icon => _currentPlanet?.GetIcon(_currentPlanet.IsUnlocked);
        public string UpgradePrice => _currentPlanet.IsMaxLevel ? "Max Level" : _currentPlanet?.Price.ToString() ?? "0";

        public bool CanUpgrade => !_currentPlanet.IsMaxLevel &&
                                  (_currentPlanet != null && _currentPlanet.Price <= _moneyStorage.Money);

        private readonly GameScreenPresenter _gameScreenPresenter;
        private readonly MoneyStorage _moneyStorage;
        private Planet _currentPlanet;

        public PlanetPopupPresenter(MoneyStorage storage, GameScreenPresenter gameScreenPresenter)
        {
            _moneyStorage = storage;
            _gameScreenPresenter = gameScreenPresenter;
        }

        public void Initialize() => _moneyStorage.OnMoneyChanged += HandleMoneyChanged;

        public void Dispose() => _moneyStorage.OnMoneyChanged -= HandleMoneyChanged;

        public void OnCloseClicked() => _gameScreenPresenter.HidePlanetPopup();

        public void OnUpgradeClicked()
        {
            if (_currentPlanet.Upgrade())
                OnUpgraded?.Invoke();
        }

        public void SetPlanet(Planet planet)
        {
            _currentPlanet = planet;
        }

        private void HandleMoneyChanged(int newValue, int prevValue) => OnMoneyChanged?.Invoke();
    }
}