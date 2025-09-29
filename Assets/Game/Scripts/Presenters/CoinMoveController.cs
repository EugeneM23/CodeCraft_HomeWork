using Game.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Game
{
    public class CoinMoveController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private float _speed = 1f;
        [SerializeField] private MoveAnimation _moveAnimation;

        private IPlanetPresenter _planetPresenter;
        private MoneyWidgetView _widgetView;

        [Inject]
        private void Construct(MoneyWidgetView widgetView, IPlanetPresenter planetPresenter)
        {
            _widgetView = widgetView;
            _planetPresenter = planetPresenter;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _moveAnimation.MoveToTarget(_widgetView.CoinTarget.position, _speed,
                () => _planetPresenter.Planet.GatherIncome());
        }
    }
}