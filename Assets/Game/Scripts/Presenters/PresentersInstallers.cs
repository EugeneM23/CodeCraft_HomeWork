using Game.Views;
using Game.Views.GameScreeen;
using UnityEngine;
using Zenject;

namespace Game.Presenters
{
    [CreateAssetMenu(
        fileName = "PresentersInstallers",
        menuName = "Zenject/New PresentersInstallers"
    )]
      public sealed class PresentersInstallers : ScriptableObjectInstaller
    {
        public override void InstallBindings()
        {
            //TODO:
            Container
                .BindInterfacesAndSelfTo<MoneyWidgetPresenter>()
                .AsSingle()
                .NonLazy();

            Container
                .BindInterfacesAndSelfTo<PlanetPopupPresenter>()
                .AsSingle()
                .NonLazy();
            
            
            
            Container
                .BindInterfacesAndSelfTo<GameScreenPresenter>()
                .AsSingle()
                .NonLazy();

            Container
                .Bind<PlanetPopupShower>()
                .AsSingle();
        }
    }
}