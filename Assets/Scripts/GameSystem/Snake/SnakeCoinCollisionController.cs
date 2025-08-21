using System;
using Modules;
using UnityEngine;
using Zenject;

namespace Game
{
    public class SnakeCoinCollisionController : IInitializable, IDisposable
    {
        private readonly Snake _snake;
        private readonly CoinManager _coinManager;

        public SnakeCoinCollisionController(Snake snake, CoinManager coinManager)
        {
            _snake = snake;
            _coinManager = coinManager;
        }

        public void Initialize() => _snake.OnMoved += CheckCoinCollision;

        public void Dispose() => _snake.OnMoved -= CheckCoinCollision;

        private void CheckCoinCollision(Vector2Int position)
        {
            if (_coinManager.TryPickCoin(position, out Coin coin)) 
                _snake.Expand(coin.Bones);
        }
    }
}