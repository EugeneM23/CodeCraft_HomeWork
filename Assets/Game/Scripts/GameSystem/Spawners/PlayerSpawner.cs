using System;
using Gameplay;
using Modules.PlayerController;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Scripts.GameObject.Player
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private CharacterController2D character;
        [Inject] private readonly PlayerCharacterProvider _playerCharacterProvider;

        private void OnEnable() => Spawn();

        public void Spawn()
        {
            var player = SceneContext.Instance.Container.InstantiatePrefab(character, transform.position, Quaternion.identity);
            _playerCharacterProvider.SetCharacter(player);
        }
    }
}