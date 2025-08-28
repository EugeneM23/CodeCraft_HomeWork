using Game.Views;
using Modules.Planets;
using UnityEngine;
using Zenject;

namespace Game
{
    public class PlanetViewInstaller : MonoInstaller
    {
        [Header("Planet")] 
        [SerializeField] private PlanetConfig _config;

        [SerializeField] private PlanetView _planetViewPrefab;

        [Header("UI Components")] [SerializeField]
        private PriceView _priceView;

        [SerializeField] private LockView _lockView;
        [SerializeField] private IncomeView _incomeView;

        [Header("Coin System")] [SerializeField]
        private CoinView _coinView;

        [SerializeField] private MoveAnimation _coinMoveAnimation;

        private readonly PlanetCatalog _planetCatalog;

        public override void InstallBindings()
        {
            BindPlanet();
            BindPrice();
            BindLock();
            BindProgressBar();
            BindCoin();
        }

        private void BindPlanet()
        {
            Container
                .BindInterfacesAndSelfTo<Planet>()
                .AsSingle()
                .WithArguments(_config)
                .NonLazy();

            Container
                .Bind<PlanetView>()
                .FromInstance(_planetViewPrefab)
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<PlanetViewPresenter>()
                .AsSingle()
                .NonLazy();
        }

        private void BindPrice()
        {
            Container
                .Bind<PriceView>()
                .FromInstance(_priceView)
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<PriceViewPresenter>()
                .AsSingle()
                .NonLazy();
        }

        private void BindLock()
        {
            Container
                .Bind<LockView>()
                .FromInstance(_lockView)
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<LockViewPresenter>()
                .AsSingle()
                .NonLazy();
        }

        private void BindProgressBar()
        {
            Container
                .Bind<IncomeView>()
                .FromInstance(_incomeView)
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<IncomeViewPresenter>()
                .AsSingle()
                .NonLazy();
        }

        private void BindCoin()
        {
            Container
                .BindInterfacesAndSelfTo<CoinViewPresenter>()
                .AsSingle()
                .NonLazy();

            Container
                .Bind<MoveAnimation>()
                .FromInstance(_coinMoveAnimation)
                .AsSingle()
                .NonLazy();
        }
    }
}