using Game.Views;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PlanetGameObjectInstaller : MonoInstaller
    {
        [SerializeField] private PlanetConfig _config;
        [SerializeField] private PlanetView _planetViewPrefab;

        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<Planet>()
                .AsSingle()
                .WithArguments(_config)
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<PlanetPresenter>()
                .AsSingle()
                .NonLazy();
        }
    }
}