using System;
using Modules;
using SnakeGame;
using UnityEngine;
using Zenject;

namespace Game
{
    public class SnakeBoundsCollisionController : IInitializable, IDisposable
    {
        private readonly WorldBounds _bounds;
        private readonly Snake _snake;
        private readonly GameCycle _gameCycle;

        public SnakeBoundsCollisionController(WorldBounds bounds, Snake snake, GameCycle gameCycle)
        {
            _bounds = bounds;
            _snake = snake;
            _gameCycle = gameCycle;
        }

        public void Initialize() => _snake.OnMoved += CheckBounds;

        public void Dispose() => _snake.OnMoved -= CheckBounds;

        private void CheckBounds(Vector2Int position)
        {
            if (!_bounds.IsInBounds(position))
                _gameCycle.GameOver();
        }
    }
}