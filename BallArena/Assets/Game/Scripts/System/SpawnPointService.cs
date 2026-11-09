using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Правило «куда ставить игрока» вынесено из сетевого кода: меняется часто,
    /// а обработчик PlayerJoined трогать не хочется.
    /// </summary>
    public sealed class SpawnPointService : MonoBehaviour
    {
        [SerializeField]
        private Transform _pointLeft;

        [SerializeField]
        private Transform _pointRight;

        public Transform GetSpawnPoint(int teamIndex)
        {
            return teamIndex == 0 ? _pointLeft : _pointRight;
        }
    }
}
