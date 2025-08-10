using Modules;
using SnakeGame;
using UnityEngine;

namespace Game
{
    public class CoinSpawner : ILevelStartListener
    {
        private readonly WorldBounds _worldBounds;
        private readonly CoinManager _coinManager;
        private readonly Difficulty _difficulty;
        private readonly CoinMemoryPool _coinMemoryPool;

        public CoinSpawner(WorldBounds worldBounds, CoinManager coinManager, Difficulty difficulty,
            CoinMemoryPool coinMemoryPool)
        {
            _worldBounds = worldBounds;
            _coinManager = coinManager;
            _difficulty = difficulty;
            _coinMemoryPool = coinMemoryPool;
        }

        public void SpawnCoins()
        {
            for (int i = 0; i < _difficulty.Current; i++)
            {
                Vector2Int position = _worldBounds.GetRandomPosition();
                Coin coin = _coinMemoryPool.Spawn();
                coin.Generate();
                coin.gameObject.transform.position = new Vector3(position.x, position.y, 0);
                _coinManager.Add(coin);
            }
        }

        public void StartLevel()
        {
            SpawnCoins();
        }
    }
}