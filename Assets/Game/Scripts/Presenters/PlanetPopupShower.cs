using Game.Views;
using Modules.Planets;

namespace Game
{
    public class PlanetPopupShower
    {
        private readonly IPlanetPopupPresenter _presenter;

        public PlanetPopupShower(IPlanetPopupPresenter presenter)
        {
            _presenter = presenter;
        }

        public void ShowPopup(Planet planet)
        {
            _presenter.SetPlanet(planet);
        }
    }
}