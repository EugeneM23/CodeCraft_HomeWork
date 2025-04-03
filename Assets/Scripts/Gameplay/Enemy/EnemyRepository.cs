using UnityEngine;

namespace Gameplay
{
    internal class EnemyRepository : MonoBehaviour
    {
        [SerializeField] private Character[] _enemies;

        public GameObject GetEnemy()
        {
            return _enemies[Random.Range(0, _enemies.Length)].gameObject;
        }
    }
}