using System;
using Modules;
using SnakeGame;
using UnityEngine;
using Zenject;

namespace Game
{
    public class SnakeCollisionComponent : IInitializable
    {
        public event Action<Coin> CoinPicked;
        public event Action OnDeath;

        private readonly WorldBounds _bounds;
        private readonly Snake _snake;
        private readonly CoinManager _coinManager;

        public SnakeCollisionComponent(WorldBounds bounds, Snake snake, CoinManager coinManager)
        {
            _bounds = bounds;
            _snake = snake;
            _coinManager = coinManager;
        }

        public void Initialize()
        {
            _snake.OnMoved += CheckBounds;
            _snake.OnMoved += CheckCoinCollision;
        }

        private void CheckBounds(Vector2Int position)
        {
            if (!_bounds.IsInBounds(position))
            {
                OnDeath?.Invoke();
            }
        }

        private void CheckCoinCollision(Vector2Int position)
        {
            foreach (Coin item in _coinManager)
                if (position == item.Position)
                {
                    CoinPicked?.Invoke(item);
                    _coinManager.Remove(item);
                    break;
                }
        }
    }
}