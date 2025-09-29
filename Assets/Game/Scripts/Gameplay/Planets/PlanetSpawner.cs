using Game.Views;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game.Gameplay
{
    public class PlanetSpawner : IInitializable
    {
        private readonly RectTransform _parent;
        private readonly Planet[] _planets;
        private readonly PlanetView _planetPrefab;
        private readonly DiContainer _container;

        public PlanetSpawner(Planet[] planets, PlanetView planetPrefab, DiContainer container, RectTransform parent)
        {
            _planets = planets;
            _planetPrefab = planetPrefab;
            _container = container;
            _parent = parent;
        }

        public void Initialize()
        {
            foreach (var item in _planets)
            {
                GameObject planet = _container.InstantiatePrefab(_planetPrefab, _parent);
                GameObjectContext context = planet.GetComponent<GameObjectContext>();
                IPlanetPresenter presenter = context.Container.Resolve<IPlanetPresenter>();
                presenter.SetPlanet(item);
            }
        }
    }
}