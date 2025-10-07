using UnityEngine;
using Zenject;

namespace Game.Views
{
    public sealed class ViewsInstaller : MonoInstaller
    {
        [SerializeField] private MoneyView moneyView;
        [SerializeField] private PlanetPopupView _planetPopupView;
        [SerializeField] private GameScreenView _gameScreenView;

        public override void InstallBindings()
        {
            //TODO:

            Container
                .Bind<MoneyView>()
                .FromInstance(moneyView)
                .AsSingle()
                .NonLazy();

            Container
                .Bind<PlanetPopupView>()
                .FromInstance(_planetPopupView)
                .AsSingle()
                .NonLazy();

            Container
                .Bind<GameScreenView>()
                .FromInstance(_gameScreenView)
                .AsSingle()
                .NonLazy();
        }
    }
}