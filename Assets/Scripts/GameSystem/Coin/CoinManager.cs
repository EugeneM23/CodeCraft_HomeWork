using System;
using System.Collections;
using System.Collections.Generic;
using Modules;
using SnakeGame;
using UnityEngine;

namespace Game
{
    public class CoinManager : IEnumerable<Coin>
    {
        public event Action OnCoinsEmpty;
        public event Action<Coin> OnCoinPicked;

        private readonly CoinMemoryPool _coinMemoryPool;
        private readonly WorldBounds _worldBounds;

        private List<Coin> _coins = new();

        public CoinManager(CoinMemoryPool coinMemoryPool, WorldBounds worldBounds)
        {
            _coinMemoryPool = coinMemoryPool;
            _worldBounds = worldBounds;
        }

        public void Remove(Coin item)
        {
            _coins.Remove(item);
            _coinMemoryPool.Despawn(item);

            if (_coins.Count == 0)
                OnCoinsEmpty?.Invoke();
        }

        public void SpawnCoin(int count)
        {
            for (int i = 0; i < count; i++)
                SpawnCoin();
        }

        private void SpawnCoin()
        {
            Vector2Int position = _worldBounds.GetRandomPosition();
            Coin coin = _coinMemoryPool.Spawn(position);
            _coins.Add(coin);
        }

        public IEnumerator<Coin> GetEnumerator()
        {
            return _coins.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryPickCoin(Vector2Int position, out Coin coin)
        {
            coin = null;

            foreach (Coin item in _coins)
            {
                if (position == item.Position)
                {
                    coin = item;
                    OnCoinPicked?.Invoke(coin);
                    Remove(coin);
                    return true;
                }
            }

            return false;
        }
    }
}