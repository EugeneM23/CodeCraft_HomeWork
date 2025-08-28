using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Zenject;

namespace Game.Views
{
    public class MoneyWidgetView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _count;
        [field: SerializeField] public Transform CoinTarget { get; private set; }

        [Inject] private readonly IMoneyWidgetPresenter _presenter;

        private void OnEnable()
        {
            _presenter.OnMoneyChanged += UpdateCount;
        }

        private void OnDisable()
        {
            _presenter.OnMoneyChanged -= UpdateCount;
        }

        public void UpdateCount(int newValue, int prevValue)
        {
            int currentValue = prevValue;

            DOTween.To(() => currentValue, x =>
                {
                    currentValue = x;
                    _count.text = currentValue.ToString();
                }, newValue, 1f)
                .SetEase(Ease.OutQuad);
        }
    }
}