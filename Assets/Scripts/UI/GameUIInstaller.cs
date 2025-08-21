using Modules;
using SnakeGame;
using UnityEngine;
using Zenject;

namespace Game
{
    public class GameUIInstaller : MonoInstaller
    {
        [SerializeField] private GameUI _gameUI;

        public override void InstallBindings()
        {
            Container
                .BindInterfacesAndSelfTo<ScoreUIPresenter>()
                .AsSingle()
                .NonLazy();

            Container.Bind<GameUI>()
                .FromInstance(_gameUI)
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<GameStatePresenter>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<Score>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<DifficultyUIPresenter>()
                .AsSingle()
                .NonLazy();
        }
    }
}