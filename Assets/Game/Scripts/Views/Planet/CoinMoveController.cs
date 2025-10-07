using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Game.Views
{
    public class CoinMoveController : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private float _speed = 1f;
        [SerializeField] private MoveAnimation _moveAnimation;

        [Inject] private MoneyView _view;
        private IPlanetPresenter _planetPresenter;

        public void Construct(IPlanetPresenter planetPresenter) => _planetPresenter = planetPresenter;

        public void OnPointerClick(PointerEventData eventData)
        {
            _moveAnimation.MoveToTarget(_view.CoinTarget.position, _speed, _planetPresenter.GatherIncome);
        }
    }
}