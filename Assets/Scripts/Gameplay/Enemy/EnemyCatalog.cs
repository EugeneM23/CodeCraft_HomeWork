using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(fileName = "EnemyCatalog_", menuName = "Enemy/EnemyCatalog")]
    internal class EnemyCatalog : ScriptableObject
    {
        [SerializeField] private Character[] _enemies;

        public GameObject GetEnemy()
        {
            return _enemies[Random.Range(0, _enemies.Length)].gameObject;
        }
    }
}