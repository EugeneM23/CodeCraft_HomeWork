using System;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Счёт, спавн мяча и зачёт гола. Живёт на префабе, который хост спавнит при входе
    /// первого игрока: сценовые NetworkObject на Fusion 2.1.3 в этом проекте не
    /// регистрируются (остаются с NetworkTypeId = None), а префабы работают.
    /// Точки и зоны берутся из сценового маркера <see cref="ArenaMarkers"/>.
    ///
    /// Гол определяется проверкой позиции мяча на попадание в AABB ворот, а не
    /// OnTriggerEnter: колбэк Unity-физики не воспроизводится при ресимуляции и
    /// читает Physics.defaultPhysicsScene вместо физической сцены раннера.
    /// Проверка позиции — чистая функция от состояния, одинаковая в любом повторе тика.
    ///
    /// Порядок относительно Physics.Simulate внутри тика задать нечем: атрибутов
    /// [OrderBefore]/[OrderAfter] в Fusion 2 не существует. Для зачёта гола это
    /// безразлично — максимум один тик задержки, а от двойного зачёта защищает _goalLockout.
    /// </summary>
    public sealed class MatchManager : NetworkBehaviour
    {
        [SerializeField]
        private NetworkObject _ballPrefab;

        [SerializeField]
        private Vector3 _goalHalfExtents = new Vector3(1f, 2f, 3.2f);

        [SerializeField]
        private float _goalLockoutSeconds = 1.5f;

        [Networked]
        [OnChangedRender(nameof(OnScoreChanged))]
        public int ScoreLeft { get; set; }

        [Networked]
        [OnChangedRender(nameof(OnScoreChanged))]
        public int ScoreRight { get; set; }

        /// <summary>Защита от двойного зачёта одного и того же гола.</summary>
        [Networked]
        private TickTimer _goalLockout { get; set; }

        /// <summary>Хостовая ссылка: клиентам она не нужна, реплицировать нечего.</summary>
        private Ball _ball;

        private ArenaMarkers _markers;

        /// <summary>
        /// Событие статическое, потому что UI лежит в сцене и не может сослаться на
        /// объект, которого в момент загрузки сцены ещё нет.
        /// </summary>
        public static event Action<int, int> ScoreChanged;

        public override void Spawned()
        {
            _markers = FindAnyObjectByType<ArenaMarkers>();

            if (_markers == null)
            {
                Debug.LogError("[MatchManager] В сцене нет ArenaMarkers — матч не поедет.");
                return;
            }

            this.OnScoreChanged();

            if (!this.HasStateAuthority)
                return;

            NetworkObject ballObject = this.Runner.Spawn(
                _ballPrefab,
                _markers.BallSpawnPoint.position,
                Quaternion.identity);

            if (!ballObject.TryGetComponent(out _ball))
                Debug.LogError("[MatchManager] В _ballPrefab положили префаб без компонента Ball.");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ScoreChanged = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!this.HasStateAuthority || _ball == null || _markers == null)
                return;

            if (!_goalLockout.ExpiredOrNotRunning(this.Runner))
                return;

            Vector3 ballPosition = _ball.transform.position;

            // Мяч в левых воротах — очко правому игроку, и наоборот.
            if (Inside(ballPosition, _markers.GoalLeft.position, _goalHalfExtents))
            {
                this.ScoreRight++;
                this.ResetRound();
            }
            else if (Inside(ballPosition, _markers.GoalRight.position, _goalHalfExtents))
            {
                this.ScoreLeft++;
                this.ResetRound();
            }
        }

        private static bool Inside(Vector3 point, Vector3 center, Vector3 halfExtents)
        {
            Vector3 delta = point - center;
            return Mathf.Abs(delta.x) <= halfExtents.x
                && Mathf.Abs(delta.y) <= halfExtents.y
                && Mathf.Abs(delta.z) <= halfExtents.z;
        }

        private void ResetRound()
        {
            _goalLockout = TickTimer.CreateFromSeconds(this.Runner, _goalLockoutSeconds);

            _ball.ResetTo(_markers.BallSpawnPoint.position);

            foreach (PlayerRef player in this.Runner.ActivePlayers)
            {
                if (!this.Runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                    continue;

                if (!playerObject.TryGetComponent(out PlayerIdentity identity))
                    continue;

                if (playerObject.TryGetComponent(out Rigidbody body))
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                if (!playerObject.TryGetComponent(out NetworkRigidbody3D networkBody))
                    continue;

                Transform point = _markers.SpawnPoints.GetSpawnPoint(identity.TeamIndex);
                networkBody.Teleport(point.position, point.rotation);
            }
        }

        private void OnScoreChanged()
        {
            Debug.Log($"[BallArena] Счёт {this.ScoreLeft} : {this.ScoreRight}");
            ScoreChanged?.Invoke(this.ScoreLeft, this.ScoreRight);
        }

        /// <summary>
        /// У зоны зачёта нет ни коллайдера, ни хитбокса — это просто AABB в коде.
        /// Гизмо здесь не украшение, а единственный способ увидеть её в редакторе.
        /// </summary>
        private void OnDrawGizmos()
        {
            if (_markers == null)
                return;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);

            if (_markers.GoalLeft != null)
                Gizmos.DrawWireCube(_markers.GoalLeft.position, _goalHalfExtents * 2f);

            if (_markers.GoalRight != null)
                Gizmos.DrawWireCube(_markers.GoalRight.position, _goalHalfExtents * 2f);
        }
    }
}
