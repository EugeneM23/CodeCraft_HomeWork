using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Маркер сцены: точки и зоны, которые нужны MatchManager'у. MatchManager живёт на
    /// префабе, а префаб не может ссылаться на объекты сцены — поэтому он находит этот
    /// маркер в рантайме. Тот же приём, что WorldTransform в гл. 03.
    ///
    /// Почему MatchManager не сценовый объект: на Fusion 2.1.3 сценовые NetworkObject
    /// в этом проекте не регистрируются вовсе — остаются с NetworkTypeId = None.
    /// Заспавненные префабы работают, поэтому матч живёт на префабе.
    /// </summary>
    public sealed class ArenaMarkers : MonoBehaviour
    {
        [SerializeField]
        private Transform _ballSpawnPoint;

        [SerializeField]
        private Transform _goalLeft;

        [SerializeField]
        private Transform _goalRight;

        [SerializeField]
        private SpawnPointService _spawnPoints;

        public Transform BallSpawnPoint => _ballSpawnPoint;

        public Transform GoalLeft => _goalLeft;

        public Transform GoalRight => _goalRight;

        public SpawnPointService SpawnPoints => _spawnPoints;
    }
}
