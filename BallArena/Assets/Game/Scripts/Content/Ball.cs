using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Мяч не содержит игровой логики: он просто физический объект, которым владеет хост.
    /// Гол считает MatchManager, а не мяч — так проверка остаётся чистой функцией от состояния.
    /// </summary>
    public sealed class Ball : NetworkBehaviour
    {
        [SerializeField]
        private Rigidbody _rigidbody;

        [SerializeField]
        private NetworkRigidbody3D _networkRigidbody;

        /// <summary>Включается аргументом -diag: печатает позицию мяча раз в секунду.</summary>
        private static readonly bool Diagnostics =
            System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-diag") >= 0;

        private float _nextDiagnosticTime;

        public override void Spawned()
        {
            this.Runner.SetIsSimulated(this.Object, true);
        }

        public override void Render()
        {
            if (!Diagnostics || Time.unscaledTime < _nextDiagnosticTime)
                return;

            _nextDiagnosticTime = Time.unscaledTime + 1f;

            Debug.Log($"[diag] tick={this.Runner.Tick} " +
                      $"role={(this.Runner.IsServer ? "host" : "client")} " +
                      $"proxy={this.Object.IsProxy} " +
                      $"ball={this.transform.position.ToString("F2")} " +
                      $"players={this.Runner.SessionInfo.PlayerCount}");
        }

        /// <summary>
        /// Телепорт, а не присвоение position: иначе интерполяция протащит визуал
        /// через всю арену. Только под StateAuthority.
        /// </summary>
        public void ResetTo(Vector3 position)
        {
            if (!this.HasStateAuthority)
                return;

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _networkRigidbody.Teleport(position, Quaternion.identity);
        }
    }
}
