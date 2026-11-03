using System.Collections.Generic;
using System.Text;
using Atomic.Entities;
using Modules.Gameplay;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// Debug readout for the playground scene: the state the game has no UI for.
    /// </summary>
    /// <remarks>
    /// The contexts are wired by the generator instead of being reached through
    /// <c>GameContext.Instance</c>, so the overlay cannot race the singleton, and every read goes
    /// through TryGet so an uninitialised context shows blanks rather than throwing out of OnGUI.
    /// </remarks>
    public sealed class PlaygroundOverlay : MonoBehaviour
    {
        private const string Keys =
            "WASD move · mouse aim · Space fire · V jump · E interact · G drop · " +
            "LShift dash · T+LMB teleport · R stun · M spawn enemy";

        [SerializeField] private GameContext _gameContext;
        [SerializeField] private PlayerContext _playerContext;

        [SerializeField] private string[] _stationNames = new string[0];
        [SerializeField] private Vector3[] _stationPositions = new Vector3[0];

        private readonly StringBuilder _text = new();
        private GUIStyle _style;

        private void OnGUI()
        {
            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };

            _text.Clear();
            this.AppendState(_text);

            var area = new Rect(10f, 10f, 460f, 200f);
            GUI.Box(area, GUIContent.none);
            GUI.Label(new Rect(area.x + 8f, area.y + 6f, area.width - 16f, area.height - 12f),
                _text.ToString(), _style);
        }

        private void AppendState(StringBuilder sb)
        {
            IEntity character = this.GetCharacter(out PlayerContext playerContext);

            sb.Append("Station: ").AppendLine(this.NearestStation(character));

            this.AppendCharacter(sb, character);
            this.AppendMana(sb, playerContext);
            this.AppendWeapon(sb, character);
            this.AppendBuffs(sb, character);
            this.AppendWorld(sb);

            sb.AppendLine();
            sb.Append(Keys);
        }

        private IEntity GetCharacter(out PlayerContext playerContext)
        {
            playerContext = _playerContext;

            if (playerContext == null)
                return null;

            return playerContext.TryGetCharacter(out var character) ? character.Value : null;
        }

        private string NearestStation(IEntity character)
        {
            if (_stationNames.Length == 0 || _stationNames.Length != _stationPositions.Length)
                return "—";

            if (character == null || !character.TryGetTransform(out Transform transform) || transform == null)
                return "—";

            Vector3 position = transform.position;
            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _stationPositions.Length; i++)
            {
                float distance = (position - _stationPositions[i]).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                best = i;
            }

            return _stationNames[best];
        }

        private void AppendCharacter(StringBuilder sb, IEntity character)
        {
            if (character == null)
            {
                sb.AppendLine("No character on the player context");
                return;
            }

            sb.Append("HP ");
            if (character.TryGetHealth(out Health health))
                sb.Append(health.GetCurrent()).Append('/').Append(health.GetMax());
            else
                sb.Append('—');

            sb.Append("   Jump sources ")
                .Append(character.TryGetJumpSourceCount(out int sources) ? sources : 0);

            sb.Append("   Grounded ")
                .AppendLine(JumpUseCase.IsGrounded(character) ? "yes" : "no");
        }

        private void AppendMana(StringBuilder sb, PlayerContext playerContext)
        {
            sb.Append("Mana ");

            if (playerContext != null
                && playerContext.TryGetCurrentMana(out var current)
                && playerContext.TryGetMaxMana(out var max))
                sb.Append(current.Value).Append('/').AppendLine(max.Value.ToString());
            else
                sb.AppendLine("—");
        }

        private void AppendWeapon(StringBuilder sb, IEntity character)
        {
            sb.Append("Weapon ");

            if (character == null || !character.TryGetWeapon(out var weaponSource) || weaponSource.Value == null)
            {
                sb.AppendLine("—");
                return;
            }

            IEntity weapon = weaponSource.Value;

            sb.Append(weapon.Name);
            sb.Append(weapon.HasRangeWeaponTag() ? " (range)" : weapon.HasMeleeWeaponTag() ? " (melee)" : "");

            sb.Append("   Ammo ");
            sb.AppendLine(weapon.TryGetAmmo(out Ammo ammo) ? ammo.GetCount().ToString() : "—");
        }

        private void AppendBuffs(StringBuilder sb, IEntity character)
        {
            sb.Append("Buffs: ");

            if (character == null || !character.TryGetBuffsEffects(out var effects))
            {
                sb.AppendLine("none");
                return;
            }

            // Through IList: IReactiveList implements both IList and IReadOnlyReactiveList, so
            // Count and the indexer are ambiguous on the interface itself.
            IList<BuffBase> buffs = effects;

            if (buffs.Count == 0)
            {
                sb.AppendLine("none");
                return;
            }

            for (int i = 0; i < buffs.Count; i++)
            {
                if (i > 0)
                    sb.Append(", ");

                sb.Append(string.IsNullOrEmpty(buffs[i].Name) ? "<unnamed>" : buffs[i].Name);
            }

            sb.AppendLine();
        }

        private void AppendWorld(StringBuilder sb)
        {
            if (_gameContext == null)
            {
                sb.AppendLine("No game context");
                return;
            }

            sb.Append("Enemies ")
                .AppendLine(_gameContext.TryGetEnemyFilter(out var enemies) ? enemies.Count.ToString() : "—");

            if (!_gameContext.TryGetGameFactory(out GameFactory factory))
                return;

            sb.Append("Pools: ");

            bool first = true;
            foreach (var pool in factory.Pools)
            {
                if (!first)
                    sb.Append(" · ");

                sb.Append(pool.Key).Append(' ').Append(pool.Value.Count);
                first = false;
            }

            if (first)
                sb.Append("none");

            sb.AppendLine();
        }

        /// <summary>Called by the playground generator.</summary>
        public void Configure(GameContext gameContext, PlayerContext playerContext, string[] names, Vector3[] positions)
        {
            _gameContext = gameContext;
            _playerContext = playerContext;
            _stationNames = names;
            _stationPositions = positions;
        }
    }
}
