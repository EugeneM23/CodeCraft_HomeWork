using System;
using Game.Views;
using Modules.Money;
using Zenject;

namespace Game.Presenters
{
    public class MoneyPresenter : IMoneyPresenter, IInitializable, IDisposable
    {
        public event Action<int, int> OnMoneyChanged;
        public string Money => _storage.Money.ToString();

        private readonly MoneyStorage _storage;

        public MoneyPresenter(MoneyStorage storage)
        {
            _storage = storage;
        }

        public void Initialize()
        {
            _storage.OnMoneyChanged += UpdateCount;
            UpdateCount(_storage.Money, _storage.Money);
        }

        public void Dispose() => _storage.OnMoneyChanged -= UpdateCount;

        public void UpdateCount(int newValue, int prevValue)
        {
            OnMoneyChanged?.Invoke(newValue, prevValue);
        }
    }
}