using Fusion;
using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Цвет команды и ник над головой. Визуал меняется только в рендер-фазе
    /// через [OnChangedRender], в FixedUpdateNetwork визуальной работы нет.
    /// </summary>
    public sealed class PlayerIdentity : NetworkBehaviour
    {
        [SerializeField]
        private Renderer _bodyRenderer;

        [SerializeField]
        private Nameplate _nameplate;

        [SerializeField]
        private Color _teamLeftColor = new Color(0.25f, 0.55f, 1f);

        [SerializeField]
        private Color _teamRightColor = new Color(1f, 0.45f, 0.2f);

        [Networked]
        [OnChangedRender(nameof(OnIdentityChanged))]
        public int TeamIndex { get; set; }

        [Networked]
        [OnChangedRender(nameof(OnIdentityChanged))]
        public NetworkString<_16> Nick { get; set; }

        /// <summary>
        /// Зовётся из onBeforeSpawned на хосте — единственное место, где можно писать
        /// [Networked] до первой репликации. Ник хост узнаёт из ConnectionToken, который
        /// клиент прислал при подключении (гл. 09), поэтому RPC не нужен вовсе.
        /// </summary>
        public void Init(int teamIndex, string nick)
        {
            this.TeamIndex = teamIndex;
            this.Nick = string.IsNullOrWhiteSpace(nick) ? $"Игрок {teamIndex + 1}" : nick;
        }

        public override void Spawned()
        {
            // OnChangedRender стреляет только на изменении, начальное значение — руками.
            this.OnIdentityChanged();
        }

        private void OnIdentityChanged()
        {
            if (_bodyRenderer != null)
                _bodyRenderer.material.color = this.TeamIndex == 0 ? _teamLeftColor : _teamRightColor;

            if (_nameplate != null)
                _nameplate.SetText(this.Nick.Value);
        }
    }
}
