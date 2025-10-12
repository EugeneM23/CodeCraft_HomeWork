using System;
using UnityEngine;

namespace Gameplay
{
    public class PlaewSpaner : MonoBehaviour
    {
        [SerializeField] private Transform _player;

        private void Start()
        {
            DiContainer.Instance.InstantiatePrefab(_player, transform.position, Quaternion.identity);
        }
    }
}