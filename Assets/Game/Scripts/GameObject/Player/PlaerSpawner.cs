using System;
using Game.Scripts.Player;
using UnityEngine;

namespace Gameplay
{
    public class PlaerSpawner : MonoBehaviour
    {
        [SerializeField] private BoxTest _player;

        private void Start()
        {
            DiContainer.Instance.InstantiatePrefab(_player, transform.position, Quaternion.identity);
        }
    }
}