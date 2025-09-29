using System.Linq;
using Game.Views;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game.Gameplay
{
    public class PlanetSpawner : IInitializable
    {
        private readonly GameObject _spawnPoints;
        private readonly Planet[] _planets;
        private readonly PlanetView _planetPrefab;
        private readonly DiContainer _container;
        private readonly float _scaleFactor = 0.8f;

        public PlanetSpawner(
            Planet[] planets,
            PlanetView planetPrefab,
            DiContainer container,
            GameObject spawnPoints
        )
        {
            _planets = planets;
            _planetPrefab = planetPrefab;
            _container = container;
            _spawnPoints = spawnPoints;
        }

        public void Initialize()
        {
            RectTransform[] spawnPoints = _spawnPoints.GetComponentsInChildren<RectTransform>().Skip(1).ToArray();

            for (int i = 0; i < spawnPoints.Length && i < _planets.Length; i++)
            {
                GameObject planet = _container.InstantiatePrefab(_planetPrefab, spawnPoints[i].transform);
                planet.transform.position = spawnPoints[i].position;
                planet.transform.localScale *= _scaleFactor;

                GameObjectContext context = planet.GetComponent<GameObjectContext>();
                IPlanetPresenter presenter = context.Container.Resolve<IPlanetPresenter>();

                presenter.SetPlanet(_planets[i]);
            }
        }
    }
}