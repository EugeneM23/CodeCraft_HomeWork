using System;
using Gameplay;
using Modules.PlayerController;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Scripts.GameObject.Player
{
    public class SpawnTest : MonoBehaviour
    {
        [FormerlySerializedAs("_player")] [SerializeField] private CharacterController2D character;

        private void Awake()
        {
            SceneContext.Instance.Container.InstantiatePrefab(character, transform.position, Quaternion.identity);
        }
    }
}