using System;
using UnityEngine;

namespace Game
{
    public class EntryPoint : MonoBehaviour
    {
        [SerializeField] private GameContextInstaller _gameContextInstaller;

        private GameContext _gameContext;

        private void Start()
        {
            _gameContext = GameContext.Instance;
            _gameContextInstaller.Install(_gameContext);
            _gameContext.Init();
            _gameContext.Enable();
        }

        private void Update() => _gameContext.OnUpdate(Time.deltaTime);
        private void FixedUpdate() => _gameContext.OnFixedUpdate(Time.fixedDeltaTime);
        private void LateUpdate() => _gameContext.OnLateUpdate(Time.deltaTime);
        private void OnDestroy() => _gameContext.Dispose();
    }
}