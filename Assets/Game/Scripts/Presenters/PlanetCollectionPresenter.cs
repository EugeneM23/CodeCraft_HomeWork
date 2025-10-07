using System.Collections.Generic;
using Game.Views;
using Modules.Planets;

namespace Game.Presenters
{
    public class PlanetCollectionPresenter : IPlanetCollectionPresenter
    {
        private readonly List<IPlanetPresenter> _presenters = new();
        private readonly GameScreenPresenter _screenPresenter;
        private readonly PlanetPopupPresenter _popupPresenter;

        public PlanetCollectionPresenter(IPlanet[] planets, PlanetPopupPresenter popupPresenter,
            GameScreenPresenter screenPresenter)
        {
            _popupPresenter = popupPresenter;
            _screenPresenter = screenPresenter;

            foreach (Planet planet in planets)
            {
                PlanetPresenter presenter = new PlanetPresenter(planet, _screenPresenter, _popupPresenter);
                presenter.Initialize();
                _presenters.Add(presenter);
            }
        }

        public IPlanetPresenter[] GetAllPlanets() => _presenters.ToArray();
    }
}