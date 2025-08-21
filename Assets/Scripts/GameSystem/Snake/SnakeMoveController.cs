using System;
using Modules;
using UnityEngine;
using Zenject;

namespace Game
{
    public class SnakeMoveController : IInitializable, IDisposable
    {
        private readonly Snake _snake;
        private readonly InputReader _input;

        public SnakeMoveController(Snake snake, InputReader input)
        {
            _snake = snake;
            _input = input;
        }

        public void Initialize() => _input.OnMove += Move;
        public void Dispose() => _input.OnMove -= Move;

        private void Move(Vector2 direction)
        {
            if (direction == Vector2.up) _snake.Turn(SnakeDirection.UP);
            else if (direction == Vector2.down) _snake.Turn(SnakeDirection.DOWN);
            else if (direction == Vector2.left) _snake.Turn(SnakeDirection.LEFT);
            else if (direction == Vector2.right) _snake.Turn(SnakeDirection.RIGHT);
        }
    }
}