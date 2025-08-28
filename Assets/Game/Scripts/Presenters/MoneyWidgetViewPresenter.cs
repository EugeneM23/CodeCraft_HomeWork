using System;
using Game.Views;
using Modules.Money;
using UnityEngine;
using Zenject;

namespace Game
{
    public class MoneyWidgetViewPresenter : IMoneyWidgetPresenter, IInitializable, IDisposable
    {
        public event Action<int, int> OnMoneyChanged;

        private readonly MoneyStorage _storage;

        public MoneyWidgetViewPresenter(MoneyStorage storage)
        {
            _storage = storage;
        }

        public void Initialize()
        {
            _storage.OnMoneyChanged += UpdateCount;
            UpdateCount(_storage.Money, _storage.Money);
        }

        public void Dispose()
        {
            _storage.OnMoneyChanged -= UpdateCount;
        }

        public void UpdateCount(int newValue, int prevValue)
        {
            OnMoneyChanged?.Invoke(newValue, prevValue);
        }
    }
}