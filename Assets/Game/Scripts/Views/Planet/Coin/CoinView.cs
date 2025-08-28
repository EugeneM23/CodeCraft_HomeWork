using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Game.Views
{
    public class CoinView : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private Image _coin;
        [SerializeField] private float _moveDuration;

        private ICoinViewPresenter _presenter;

        [Inject]
        private void Construct(ICoinViewPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Start()
        {
            Show(_presenter.IsIncomeReady);
            _presenter.OnIncomeReady += Show;
        }

        private void OnDestroy() => _presenter.OnIncomeReady -= Show;

        private void Show(bool isActive) => gameObject.SetActive(isActive);

        public void OnPointerDown(PointerEventData eventData)
        {
            _presenter.MoveTo(_moveDuration, onComplete: () => _presenter.GatherIncome());
        }
    }
}