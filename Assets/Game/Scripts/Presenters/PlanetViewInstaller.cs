using Game.Views;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PlanetViewInstaller : MonoInstaller
    {
        [Header("Planet")] [SerializeField] private PlanetConfig _config;

        [SerializeField] private PlanetView _planetViewPrefab;

        [SerializeField] private MoveAnimation _coinMoveAnimation;

        public override void InstallBindings()
        {
            BindPlanet();
        }

        private void BindPlanet()
        {
            Container
                .Bind<MoveAnimation>()
                .FromInstance(_coinMoveAnimation)
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<Planet>()
                .AsSingle()
                .WithArguments(_config)
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<PlanetPresenter>()
                .AsSingle()
                .NonLazy();

            Container
                .Bind<PlanetView>()
                .FromInstance(_planetViewPrefab)
                .AsSingle()
                .NonLazy();
        }
    }
}