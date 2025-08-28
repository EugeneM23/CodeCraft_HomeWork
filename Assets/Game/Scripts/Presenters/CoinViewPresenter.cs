using System;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game.Views
{
    public class CoinViewPresenter : IInitializable, IDisposable, ICoinViewPresenter
    {
        public event Action<bool> OnIncomeReady;
        private readonly Planet _planet;
        private readonly MoveAnimation _moveAnimation;
        private readonly MoneyWidgetView _widgetView;
        public bool IsIncomeReady => _planet.IsIncomeReady;

        public CoinViewPresenter(Planet planet, MoveAnimation moveAnimation, MoneyWidgetView widgetView)
        {
            _planet = planet;
            _moveAnimation = moveAnimation;
            _widgetView = widgetView;
        }

        public void Initialize() => _planet.OnIncomeReady += HandleIncomeReady;

        public void Dispose() => _planet.OnIncomeReady -= HandleIncomeReady;

        private void HandleIncomeReady(bool isReady) => OnIncomeReady?.Invoke(isReady);

        public void GatherIncome() => _planet.GatherIncome();

        public void MoveTo(float duration = 1f, Action onComplete = null)
        {
            _moveAnimation.MoveToTarget(_widgetView.CoinTarget.position, duration, onComplete);
        }
    }
}