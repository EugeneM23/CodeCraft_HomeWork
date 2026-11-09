using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BallArena
{
    /// <summary>
    /// Единственная точка входа. Раннер и scene-manager лежат в сцене ДО старта игры,
    /// поэтому все остальные системы могут ссылаться на них через инспектор.
    /// </summary>
    public sealed class EntryPoint : MonoBehaviour
    {
        /// <summary>
        /// Номер сборки. Поднимай при каждой, которую отдаёшь другому игроку, —
        /// он виден на экране у обоих, и «у тебя старый билд?» перестаёт быть догадкой.
        ///
        /// Сознательно НЕ уезжает в AppSettings.AppVersion: там он расщепил бы
        /// матчмейкинг, и разные сборки молча не видели бы друг друга — ровно тот
        /// баг, который лечили с регионами. Несовпадение должно быть видно глазами.
        ///
        /// v1 — первая сборка, кнопка «Играть» не нажималась
        /// v2 — кнопка починена, регион не зафиксирован: двое из разных мест не сходились
        /// v3 — регион зашит в eu, код комнаты нормализуется, на экране регион и роль
        /// </summary>
        public const string Version = "v3";

        private const string DefaultNick = "Игрок";

        /// <summary>
        /// Регион Photon. Главное в этом файле.
        ///
        /// Если регион НЕ зафиксирован, у Photon включается Best Region: каждый клиент
        /// сам пингует регионы и уходит в ближайший (`AppSettings.IsBestRegion =>
        /// UseNameServer && string.IsNullOrEmpty(FixedRegion)`). А имя комнаты уникально
        /// ВНУТРИ региона. Поэтому двое из разных мест вводят один код, попадают в
        /// разные регионы, каждый создаёт свою комнату и становится в ней хостом —
        /// без единой ошибки на экране.
        ///
        /// На одной машине оба процесса пингуют из одной сети, попадают в один регион,
        /// и всё работает. Именно поэтому локальные тесты этот баг не ловят.
        ///
        /// Значение обязано быть ОДИНАКОВЫМ у всех игроков и зашито в билд.
        /// </summary>
        private const string DefaultRegion = "eu";

        [SerializeField]
        private NetworkRunner _runner;

        [SerializeField]
        private NetworkSceneManagerDefault _sceneManager;

        [SerializeField]
        private GameObject _connectPanel;

        [SerializeField]
        private InputField _roomCodeField;

        [SerializeField]
        private InputField _nickField;

        [SerializeField]
        private Button _playButton;

        [SerializeField]
        private Text _statusText;

        /// <summary>
        /// Строка состояния ПОВЕРХ игры. Лежит вне _connectPanel, иначе её погасит
        /// вместе с панелью и игрок никогда не увидит ни региона, ни роли.
        /// </summary>
        [SerializeField]
        private Text _hudText;

        [SerializeField]
        private string _region = DefaultRegion;

        /// <summary>
        /// Ник локального игрока. Хост узнаёт его из ConnectionToken при подключении.
        /// </summary>
        public static string LocalNick { get; private set; } = DefaultNick;

        private string _room = string.Empty;
        private float _nextHudRefresh;

        private void OnEnable()
        {
            _playButton.onClick.AddListener(this.OnPlayClicked);
        }

        private void OnDisable()
        {
            _playButton.onClick.RemoveListener(this.OnPlayClicked);
        }

        /// <summary>
        /// Аргументы командной строки, чтобы поднять две сборки без ручных кликов:
        /// BallArena.exe -room test -nick Host -region eu -autostart
        /// </summary>
        private void Start()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            bool autoStart = false;

            for (int i = 0; i < args.Length; i++)
            {
                bool hasValue = i + 1 < args.Length;

                switch (args[i])
                {
                    case "-room" when hasValue:
                        _roomCodeField.text = args[i + 1];
                        break;
                    case "-nick" when hasValue:
                        _nickField.text = args[i + 1];
                        break;
                    case "-region" when hasValue:
                        _region = args[i + 1];
                        break;
                    case "-autostart":
                        autoStart = true;
                        break;
                }
            }

            _hudText.text = string.Empty;

            if (autoStart)
                this.OnPlayClicked();
        }

        private void Update()
        {
            // Число игроков меняется после старта, поэтому строку надо перерисовывать,
            // иначе «игроков: 1» останется навсегда и будет пугать.
            if (_runner == null || !_runner.IsRunning || Time.unscaledTime < _nextHudRefresh)
                return;

            _nextHudRefresh = Time.unscaledTime + 0.5f;
            this.RefreshHud();
        }

        private void OnPlayClicked()
        {
            _ = this.StartGame();
        }

        private async Task StartGame()
        {
            // Имена комнат в Photon регистрозависимы, поэтому «Ball» и «ball» — две
            // разные комнаты. Приводим к нижнему регистру, иначе разойдётесь на опечатке.
            _room = (_roomCodeField.text ?? string.Empty).Trim().ToLowerInvariant();

            if (_room.Length == 0)
            {
                // Раньше здесь молча подставлялся дефолт, и игрок с пустым полем
                // уезжал в чужую комнату «ball», не понимая, почему он один.
                _statusText.text = "Введи код комнаты — одинаковый у вас обоих.";
                return;
            }

            LocalNick = string.IsNullOrWhiteSpace(_nickField.text) ? DefaultNick : _nickField.text.Trim();

            string region = (_region ?? string.Empty).Trim().ToLowerInvariant();

            _playButton.interactable = false;
            _statusText.text = $"Подключаюсь к «{_room}», регион {region}…";

            // Сцену надо отдавать через NetworkSceneInfo.AddSceneRef, а не присваивать
            // SceneRef напрямую: неявное приведение компилируется, но сценовые
            // NetworkObject остаются с NetworkTypeId = None и никогда не спавнятся.
            SceneRef sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
            var sceneInfo = new NetworkSceneInfo();
            if (sceneRef.IsValid)
                sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Additive);

            // StartGame возвращает Task<StartGameResult>. Не проверить result.Ok —
            // значит получить пустую сцену без единой записи в консоли.
            StartGameResult result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.AutoHostOrClient,
                SessionName = _room,
                PlayerCount = 2,
                Scene = sceneInfo,
                SceneManager = _sceneManager,

                // Регион приезжает сюда копией глобальных настроек: так его можно
                // переопределить аргументом командной строки, не правя ассет.
                CustomPhotonAppSettings = BuildAppSettings(region),

                // Ник уезжает на хост вместе с подключением. Так он известен уже
                // в onBeforeSpawned, и RPC от клиента не нужен.
                ConnectionToken = System.Text.Encoding.UTF8.GetBytes(LocalNick),
            });

            if (!result.Ok)
            {
                _playButton.interactable = true;
                _statusText.text = $"Не подключилось: {result.ShutdownReason}\n{result.ErrorMessage}";
                Debug.LogError($"StartGame failed: {result.ShutdownReason} {result.ErrorMessage}");
                return;
            }

            _statusText.text = string.Empty;
            _connectPanel.SetActive(false);

            this.RefreshHud();

            Debug.Log($"[BallArena] {Version}, комната «{_room}», регион {this.CurrentRegion()}, " +
                      $"роль {(_runner.IsServer ? "хост" : "клиент")}, " +
                      $"players={this.PlayerCount()}, connection={_runner.CurrentConnectionType}");
        }

        /// <summary>
        /// Копия глобальных настроек с подменённым регионом. Копия, а не сам объект:
        /// глобальный ассет мутировать нельзя, он один на процесс.
        /// </summary>
        private static FusionAppSettings BuildAppSettings(string region)
        {
            if (!PhotonAppSettings.TryGetGlobal(out PhotonAppSettings global))
            {
                Debug.LogError("[BallArena] PhotonAppSettings не найден — регион не зафиксирован.");
                return null;
            }

            FusionAppSettings settings = global.AppSettings.GetCopy();
            settings.FixedRegion = region;
            return settings;
        }

        private string CurrentRegion()
        {
            return _runner != null && _runner.SessionInfo ? _runner.SessionInfo.Region : "?";
        }

        private int PlayerCount()
        {
            return _runner != null && _runner.SessionInfo ? _runner.SessionInfo.PlayerCount : 0;
        }

        /// <summary>
        /// То, по чему игрок сам поймёт, что он не в той комнате: разные регионы или
        /// два «ХОСТ» на двух экранах — значит вы в разных комнатах.
        /// </summary>
        private void RefreshHud()
        {
            if (_hudText == null)
                return;

            string role = _runner.IsServer ? "ХОСТ" : "клиент";
            int players = this.PlayerCount();

            _hudText.text = $"{Version}   комната «{_room}»   регион {this.CurrentRegion()}   " +
                            $"{role}   игроков {players}/2";
            _hudText.color = players >= 2 ? new Color(0.6f, 0.9f, 0.6f) : new Color(1f, 0.85f, 0.45f);
        }
    }
}
