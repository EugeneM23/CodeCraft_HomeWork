using System;
using Gameplay;
using Modules.PlayerController;
using UnityEngine;

namespace Game.Scripts.GameObject.Player
{
    public class SpawnTest : MonoBehaviour
    {
        [SerializeField] private PlayerController _player;

        private void Awake()
        {
            SceneContext.Instance.Container.InstantiatePrefab(_player, transform.position, Quaternion.identity);
        }
    }
}