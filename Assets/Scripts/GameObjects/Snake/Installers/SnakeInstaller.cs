using Modules;
using UnityEngine;
using Zenject;

namespace Game
{
    public class SnakeInstaller : MonoInstaller
    {
        [SerializeField] private Snake _snake;

        public override void InstallBindings()
        {
            Container.Bind<Snake>().FromInstance(_snake).AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeCollisionComponent>().AsSingle().NonLazy();

            Container.BindInterfacesAndSelfTo<ScoreUIHandler>().AsSingle().NonLazy();

            Container.BindInterfacesAndSelfTo<SnakeMoveController>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeExpandHandler>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeDeathHandler>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeSpeedHandler>().AsSingle().NonLazy();
        }
    }
}