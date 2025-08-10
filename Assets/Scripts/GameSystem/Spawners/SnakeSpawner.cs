using System;
using Modules;
using UnityEngine;
using Zenject;

namespace Game
{
    public class SnakeSpawner : ILevelStartListener, IGameWinListener, IGameOverListener
    {
        private readonly Snake _snakePrefab;
        private readonly SceneContext _sceneContext;

        private GameObject _snakeInstance;

        public SnakeSpawner(Snake snakePrefab, SceneContext sceneContext)
        {
            _snakePrefab = snakePrefab;
            _sceneContext = sceneContext;
        }

        public void StartLevel() => SpawnSnake();

        public void SpawnSnake()
        {
            if (_snakeInstance != null)
                DestroySanake();

            _snakeInstance = _sceneContext.Container.InstantiatePrefab(_snakePrefab);
        }

        public void WinGame() => DestroySanake();

        private void DestroySanake() => GameObject.Destroy(_snakeInstance);

        public void FinishGame() => DestroySanake();
    }
}