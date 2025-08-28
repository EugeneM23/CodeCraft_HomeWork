using Game.Views;
using Modules.Planets;

namespace Game
{
    public class PlanetPopupShower
    {
        private readonly IPlanetPopupViewPresenter _presenter;

        public PlanetPopupShower(IPlanetPopupViewPresenter presenter)
        {
            _presenter = presenter;
        }

        public void ShowPopup(Planet planet)
        {
            _presenter.Show(planet);
        }
    }
}