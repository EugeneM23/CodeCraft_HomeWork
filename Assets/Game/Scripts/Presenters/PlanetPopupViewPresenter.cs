using System;
using ModestTree;
using Modules.Money;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game.Views
{
    public class PlanetPopupViewPresenter : IPlanetPopupViewPresenter, IInitializable, IDisposable
    {
        public event Action OnShow;
        public event Action OnMoneyChanged;
        public event Action OnUpgraded;

        private readonly MoneyStorage _moneyStorage;
        private Planet _currentPlanet;

        public string UpgradePrice
        {
            get
            {
                if (_currentPlanet.IsMaxLevel)
                    return "Max Level";

                return _currentPlanet?.Price.ToString() ?? "0";
            }
        }

        public string Population => _currentPlanet?.Population.ToString() ?? "0";
        public string Level => _currentPlanet?.Level.ToString() ?? "0";
        public string Income => _currentPlanet?.MinuteIncome.ToString() ?? "0";
        public Sprite Icon => _currentPlanet?.GetIcon(_currentPlanet.IsUnlocked);

        public bool CanUpgrade
        {
            get
            {
                if (_currentPlanet.IsMaxLevel)
                    return false;

                return _currentPlanet != null && _currentPlanet.Price <= _moneyStorage.Money;
            }
        }

        public PlanetPopupViewPresenter(MoneyStorage storage)
        {
            _moneyStorage = storage;
        }

        public void Initialize()
        {
            _moneyStorage.OnMoneyChanged += HandleMoneyChanged;
        }

        public void Dispose()
        {
            _moneyStorage.OnMoneyChanged -= HandleMoneyChanged;

            if (_currentPlanet != null)
                _currentPlanet.OnUpgraded -= HandlePlanetUpgraded;
        }

        public void Show(Planet planet)
        {
            _currentPlanet = planet;
            _currentPlanet.OnUpgraded += HandlePlanetUpgraded;

            OnShow?.Invoke();
        }

        private void HandlePlanetUpgraded(int level) => OnUpgraded?.Invoke();

        private void HandleMoneyChanged(int newValue, int prevValue) => OnMoneyChanged?.Invoke();

        public void UpgradePlanet() => _currentPlanet.Upgrade();
    }
}