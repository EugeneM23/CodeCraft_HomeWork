using System;
using UnityEngine;

namespace Game.Views
{
    public interface ICoinViewPresenter
    {
        event Action<bool> OnIncomeReady;
        bool IsIncomeReady { get; }
        void GatherIncome();
        void MoveTo(float duration = 1f, Action onComplete = null);
    }
}