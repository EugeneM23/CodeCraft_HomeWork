using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Gameplay
{
    public class PlayerDeathObserver : MonoBehaviour
    {
        private const string SceneName = "Game";
        [SerializeField] private PlayerCharacterProvider _characterProvider;

        private void OnEnable() => _characterProvider.Character.DeSpawn += OnDeath;

        private void OnDisable() => _characterProvider.Character.DeSpawn -= OnDeath;

        private void OnDeath(GameObject obj) => SceneManager.LoadScene(SceneName);
    }
}