using Modules;
using SnakeGame;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

namespace Game
{
    public class GameSystemInstaller : MonoInstaller
    {
        [Header("Prefabs")]
        [SerializeField] private Snake _snakePrefab;
        [SerializeField] private Coin _coinPrefab;

        [Header("References")]
        [SerializeField] private WorldBounds _worldBounds;

        [Header("Settings")]
        [SerializeField] private GameSettings _gameSettings;

        public override void InstallBindings()
        {
            BindSnake();
            BindCore();
            BindDifficulty();
            BindCoinFeature();
            BindScore();
        }

        private void BindSnake()
        {
            Container.BindInterfacesAndSelfTo<Snake>()
                .FromComponentInNewPrefab(_snakePrefab)
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<SnakeBoundsCollisionController>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<SnakeCoinCollisionController>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<SnakeMoveController>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<SnakeSpeedController>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<SnakeDisposeController>()
                .AsSingle()
                .NonLazy();
        }

        private void BindCore()
        {
            Container.BindInterfacesAndSelfTo<GameCycle>()
                .AsSingle()
                .NonLazy();

            Container.Bind<GameSettings>()
                .FromInstance(_gameSettings)
                .AsSingle()
                .NonLazy();

            Container.Bind<WorldBounds>()
                .FromInstance(_worldBounds)
                .AsSingle()
                .NonLazy();
        }

        private void BindDifficulty()
        {
            Container.BindInterfacesAndSelfTo<Difficulty>()
                .AsSingle()
                .WithArguments(_gameSettings.MaxLevel)
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
                .FromComponentInNewPrefab(_coinPrefab)
                .UnderTransformGroup("CoinPool");

            Container.BindInterfacesAndSelfTo<CoinManager>()
                .AsSingle()
                .NonLazy();
        }

        private void BindScore()
        {
            Container.BindInterfacesAndSelfTo<Score>()
                .AsSingle()
                .NonLazy();

            Container.BindInterfacesAndSelfTo<ScoreController>()
                .AsSingle()
                .NonLazy();
        }
    }
}