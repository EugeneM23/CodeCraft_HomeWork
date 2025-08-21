using Modules;
using SnakeGame;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

namespace Game
{
    public class GameSystemInstaller : MonoInstaller
    {
        [Header("Prefabs")] [SerializeField] private Snake _snakePrefab;
        [SerializeField] private Coin _coinPrefab;

        [Header("References")] [SerializeField]
        private WorldBounds _worldBounds;

        [FormerlySerializedAs("_gameSetings")] [Header("Settings")] [SerializeField]
        private GameSettings gameSettings;

        private readonly GameUIInstaller _gameUIInstaller;

        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<GameCycle>().AsSingle().NonLazy();

            BindSnake();
            BindSettings();
            BindWorld();
            BindDifficulty();
            BindCoinFeature();
        }

        private void BindSnake()
        {
            Container.BindInterfacesAndSelfTo<Snake>().FromComponentInNewPrefab(_snakePrefab).AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeCollisionComponent>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeMoveController>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeExpandHandler>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeDeathHandler>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<SnakeSpeedHandler>().AsSingle().NonLazy();
            Container.BindInterfacesAndSelfTo<ScoreController>().AsSingle().NonLazy();
        }

        private void BindSettings()
        {
            Container.Bind<GameSettings>()
                .FromInstance(gameSettings)
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

        private void BindDifficulty()
        {
            Container.BindInterfacesAndSelfTo<Difficulty>()
                .AsSingle()
                .WithArguments(gameSettings.MaxLevel)
                .NonLazy();

            Container.BindInterfacesAndSelfTo<DifficultyController>()
                .AsSingle()
                .NonLazy();
        }

        private void BindCoinFeature()
        {
            Container.BindInterfacesAndSelfTo<CoinSpawnController>()
                .AsSingle()
                .NonLazy();

            Container.BindMemoryPool<Coin, CoinMemoryPool>()
                .WithInitialSize(10)
                .FromComponentInNewPrefab(_coinPrefab)
                .UnderTransformGroup("CoinPool");

            Container.BindInterfacesAndSelfTo<CoinManager>()
                .AsSingle()
                .NonLazy();
        }
    }
}