using UnityEngine;
using Zenject;

namespace Game.Views
{
    public class PlanetCollectionView : MonoBehaviour
    {
        [SerializeField] private PlanetView[] _planetView;
        [Inject] private readonly IPlanetCollectionPresenter _presenter;

        private void OnEnable()
        {
            IPlanetPresenter[] presenters = _presenter.GetAllPlanets();

            for (int i = 0; i < _planetView.Length; i++)
            {
                _planetView[i].Construct(presenters[i]);
                _planetView[i].Show();
            }
        }

        private void OnDisable()
        {
            foreach (PlanetView view in _planetView)
                view.Hide();
        }
    }
}