using System;

namespace Game.Views
{
    public interface IMoneyWidgetPresenter
    {
        public event Action<int, int> OnMoneyChanged;
        void UpdateCount(int newValue, int prevValue);
    }
}