using System;

namespace Game.Views
{
    public interface IIncomeViewPresenter
    {
        event Action<float, string> OnIncomeTimeChanged;
        event Action<bool> OnStateChanged;
        bool IsPlanetUnlocked { get; }
    }
}