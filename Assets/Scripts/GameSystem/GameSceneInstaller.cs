using Modules;
using SnakeGame;
using UnityEngine;
using Zenject;

namespace Game
{
    public class GameSceneInstaller : MonoInstaller
    {
        [Header("Prefabs")] [SerializeField] private Snake _snakePrefab;
        [SerializeField] private Coin _coinPrefab;

        [Header("References")] [SerializeField]
        private WorldBounds _worldBounds;

        [SerializeField] private GameUI _gameUI;

        [Header("Settings")] [SerializeField] private GameSetings _gameSetings;

        public override void InstallBindings()
        {
            BindSettings();
            BindWorld();
            BindManagers();
            BindUI();
            BindDifficulty();
            BindSpawners();
            BindPools();
        }

        private void BindSettings()
        {
            Container.Bind<GameSetings>()
                .FromInstance(_gameSetings)
                .AsSingle()
                .NonLazy();
        }

        private void BindWorld()
        {
            Container.Bind<WorldBounds>()
                .FromInstance(_worldBounds)
                .AsSingle()
                .NonLazy();
        }

        private void BindManagers()
        {
            Container.BindInterfacesAndSelfTo<GameManager>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<CoinManager>()
                .AsSingle()
                .NonLazy();
        }

        private void BindUI()
        {
            Container.Bind<GameUI>()
                .FromInstance(_gameUI)
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<GameOverUIHandler>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<GameWinUIHandler>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<Score>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<DifficultyUIHandler>()
                .AsSingle()
                .NonLazy();
        }

        private void BindDifficulty()
        {
            Container.BindInterfacesAndSelfTo<Difficulty>()
                .AsSingle()
                .WithArguments(_gameSetings.MaxLevel)
                .NonLazy();

            Container.BindInterfacesAndSelfTo<DifficultyController>()
                .AsSingle()
                .NonLazy();
        }

        private void BindSpawners()
        {
            Container.BindInterfacesAndSelfTo<CoinSpawner>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<SnakeSpawner>()
                .AsSingle()
                .WithArguments(_snakePrefab)
                .NonLazy();
        }

        private void BindPools()
        {
            Container.BindMemoryPool<Coin, CoinMemoryPool>()
                .WithInitialSize(10)
                .FromComponentInNewPrefab(_coinPrefab)
                .UnderTransformGroup("CoinPool");
        }
    }
}