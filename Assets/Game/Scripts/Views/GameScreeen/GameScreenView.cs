using UnityEngine;
using Zenject;

namespace Game.Views
{
    public class GameScreenView : MonoBehaviour
    {
        [SerializeField] private PlanetPopupView _planetPopupView;

        [Inject] private readonly IGameScreenPresenter _presenter;

        private void OnEnable()
        {
            _presenter.OnPlanetPopupShow += _planetPopupView.Show;
            _presenter.OnPlanetPopupHide += _planetPopupView.Hide;
        }

        private void OnDisable()
        { 
            _presenter.OnPlanetPopupShow -= _planetPopupView.Show;
            _presenter.OnPlanetPopupHide -= _planetPopupView.Hide;
        }
    }
}