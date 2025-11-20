using System;
using Atomic.Entities;
using UnityEngine;

namespace Game.Gameplay
{
    [RequireComponent(typeof(Collider))]
    public sealed class EnemyTrigger : MonoBehaviour
    {
        [SerializeField] private SceneEntity[] _enemies;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetEntity(out IEntity entity) && entity.HasPlayerTag())
            {
                for (int i = 0; i < _enemies.Length; i++)
                    _enemies[i].GetTarget().Value = entity;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetEntity(out IEntity entity) && entity.HasPlayerTag())
            {
                for (int i = 0; i < _enemies.Length; i++)
                    _enemies[i].GetTarget().Value = null;
            }
        }
    }
}