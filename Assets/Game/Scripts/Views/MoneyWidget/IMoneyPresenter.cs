using System;

namespace Game.Views
{
    public interface IMoneyPresenter
    {
        public event Action<int, int> OnMoneyChanged;
        string Money { get; }
        void UpdateCount(int newValue, int prevValue);
    }
}