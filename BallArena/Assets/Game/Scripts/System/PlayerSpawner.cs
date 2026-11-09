using Fusion;
using UnityEngine;

namespace BallArena
{
    /// <summary>
    /// Server-authoritative спавн. Колбэки прилетают на все пиры, поэтому гейт IsServer
    /// обязателен: без него каждый клиент плодил бы дубль.
    /// Подписка в OnEnable, потому что StartGame зовётся в Start — Unity гарантирует,
    /// что OnEnable всех компонентов пройдёт раньше любого Start.
    /// </summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField]
        private NetworkEvents _networkEvents;

        [SerializeField]
        private SpawnPointService _spawnPoints;

        [SerializeField]
        private NetworkObject _playerPrefab;

        [SerializeField]
        private NetworkObject _matchPrefab;

        /// <summary>Хостовое состояние, вне FixedUpdateNetwork — реплицировать его не нужно.</summary>
        private readonly bool[] _teamTaken = new bool[2];

        private bool _matchSpawned;

        private void OnEnable()
        {
            _networkEvents.PlayerJoined.AddListener(this.OnPlayerJoined);
            _networkEvents.PlayerLeft.AddListener(this.OnPlayerLeft);
        }

        private void OnDisable()
        {
            _networkEvents.PlayerJoined.RemoveListener(this.OnPlayerJoined);
            _networkEvents.PlayerLeft.RemoveListener(this.OnPlayerLeft);
        }

        private void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            // Матч спавним один раз, при входе первого игрока: до старта раннера
            // спавнить нельзя, а сценовым объектом он быть не может (см. ArenaMarkers).
            if (!_matchSpawned)
            {
                _matchSpawned = true;
                runner.Spawn(_matchPrefab);
            }

            int team = _teamTaken[0] ? 1 : 0;
            if (_teamTaken[team])
            {
                Debug.LogWarning($"Комната полна, игрок {player} без персонажа.");
                return;
            }

            _teamTaken[team] = true;

            Transform point = _spawnPoints.GetSpawnPoint(team);

            string nick = ReadNick(runner, player);

            // Четвёртый аргумент — input authority. Забыть его значит получить
            // неподвижного игрока без единой ошибки в консоли.
            NetworkObject playerObject = runner.Spawn(
                _playerPrefab,
                point.position,
                point.rotation,
                player,
                onBeforeSpawned: (_, spawned) =>
                {
                    // Единственное место, где можно писать [Networked] до первой репликации.
                    if (spawned.TryGetComponent(out PlayerIdentity identity))
                        identity.Init(team, nick);
                });

            runner.SetPlayerObject(player, playerObject);

            Debug.Log($"[BallArena] Заспавнен игрок {player.PlayerId}, команда {team}.");
        }

        /// <summary>
        /// Ник хост берёт из ConnectionToken, который клиент прислал при подключении.
        /// Для собственного игрока хоста токена нет — там ник известен локально.
        /// </summary>
        private static string ReadNick(NetworkRunner runner, PlayerRef player)
        {
            if (player == runner.LocalPlayer)
                return EntryPoint.LocalNick;

            byte[] token = runner.GetPlayerConnectionToken(player);

            return token == null || token.Length == 0
                ? null
                : System.Text.Encoding.UTF8.GetString(token);
        }

        private void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer)
                return;

            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                return;

            if (playerObject.TryGetComponent(out PlayerIdentity identity))
                _teamTaken[Mathf.Clamp(identity.TeamIndex, 0, 1)] = false;

            runner.Despawn(playerObject);

            // Иначе запись в реестре висит на уничтоженный объект.
            runner.SetPlayerObject(player, null);
        }
    }
}
