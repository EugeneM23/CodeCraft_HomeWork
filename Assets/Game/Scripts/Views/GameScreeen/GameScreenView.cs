using UnityEngine;
using Zenject;

namespace Game.Views.GameScreeen
{
    public class GameScreenView : MonoBehaviour
    {
        [SerializeField] private PlanetPopupView _planetPopupView;

        [Inject] private readonly IGameScreenPresenter _presenter;

        private void OnEnable()
        {
            _presenter.OnPlanetPopupVisible += _planetPopupView.Show;
        }

        private void OnDisable()
        {
            _presenter.OnPlanetPopupVisible -= _planetPopupView.Show;
        }
    }
}