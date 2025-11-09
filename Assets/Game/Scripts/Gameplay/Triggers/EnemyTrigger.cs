using System;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public sealed class EnemyTrigger : MonoBehaviour
    {
        [SerializeField]
        private SceneEntity[] _enemies;

        private void OnTriggerEnter(Collider other)
        {
            //TODO
        }

        private void OnTriggerExit(Collider other)
        {
            //TODO
        }
    }
}