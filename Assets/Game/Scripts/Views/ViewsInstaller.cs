using UnityEngine;
using Zenject;

namespace Game.Views
{
    public sealed class ViewsInstaller : MonoInstaller
    {
        [SerializeField] private MoneyWidgetView _moneyWidgetView;
        [SerializeField] private PlanetPopupView _planetPopupView;

        public override void InstallBindings()
        {
            //TODO:

            Container
                .Bind<MoneyWidgetView>()
                .FromInstance(_moneyWidgetView)
                .AsSingle()
                .NonLazy();

            Container
                .Bind<PlanetPopupView>()
                .FromInstance(_planetPopupView)
                .AsSingle()
                .NonLazy();

            Container
                .Bind<PlanetPopupShower>()
                .AsSingle();
        }
    }
}