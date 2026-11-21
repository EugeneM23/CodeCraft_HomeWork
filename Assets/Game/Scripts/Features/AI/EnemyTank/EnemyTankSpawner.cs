using Atomic.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Prototype (the user): J in the sandbox puts an enemy tank — a copy of the player's (SandboxHordeConfig.enemyTank)
    /// driven by <see cref="EnemyTankBrain"/> — on the ground some way off the player, on a random side. «Танки: N» under
    /// the zombie counter.
    /// </summary>
    public sealed class EnemyTankSpawner : MonoBehaviour
    {
        private const float DISTANCE = 40f;
        private const float ALLY_DISTANCE = 10f;
        private const float DROP_HEIGHT = 60f;
        private const float WIDTH = 260f;
        private const float HEIGHT = 34f;
        private const float MARGIN = 12f;

        private MonoEntity _prefab;
        private readonly PlayerTank _player = new();
        private GUIStyle _style;
        private GUIStyle _buttonStyle;
        private int _shown = -1;
        private int _shownAllies = -1;
        private string _text = string.Empty;

        private Vector2 _mapMin = new(-40f, -40f);
        private Vector2 _mapMax = new(40f, 40f);

        /// <summary>The sandbox's spawner while the sandbox is up (the debug panel's button).</summary>
        public static EnemyTankSpawner Active { get; private set; }

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        public void Init(MonoEntity prefab, Vector2 mapMin, Vector2 mapMax)
        {
            _prefab = prefab;
            _mapMin = mapMin + Vector2.one * 4f;
            _mapMax = mapMax - Vector2.one * 4f;
            AllyTankBrain.MapMin = _mapMin;
            AllyTankBrain.MapMax = _mapMax;
            WormPrototype.MapMin = _mapMin;
            WormPrototype.MapMax = _mapMax;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.jKey.wasPressedThisFrame)
                Spawn();
            if (keyboard.kKey.wasPressedThisFrame)
                SpawnAlly();
            if (keyboard.uKey.wasPressedThisFrame)
                WormPrototype.Spawn();
        }

        /// <summary>An enemy tank some way off the player, on a random side, facing it.</summary>
        public EnemyTankBrain Spawn() => Place<EnemyTankBrain>("[EnemyTank]", DISTANCE, true);

        /// <summary>A friendly tank behind the player (K), following it and shooting at the enemies.</summary>
        public AllyTankBrain SpawnAlly() => Place<AllyTankBrain>("[AllyTank]", ALLY_DISTANCE, false);

        private T Place<T>(string name, float distance, bool randomSide) where T : TankAIBrain
        {
            if (_prefab == null)
            {
                Debug.LogWarning("EnemyTankSpawner: no enemyTank in SandboxHordeConfig.");
                return null;
            }

            _player.Refresh();
            Transform player = _player.Transform;
            Vector3 center = player != null ? player.position : Vector3.zero;
            Vector3 back = player != null ? -player.forward : Vector3.back;
            float behind = Mathf.Atan2(back.z, back.x);
            // Inside the map and over ground: off the map's edge it fell through the void.
            Vector3 at = default;
            bool found = false;
            float start = randomSide ? Random.value * Mathf.PI * 2f : behind;
            for (int i = 0; i < 24 && !found; i++)
            {
                // Round from the start: a random side for the enemy, behind the player first for the ally.
                int step = (i % 12 + 1) / 2 * (i % 2 == 0 ? 1 : -1);
                float a = start + step * Mathf.PI * 2f / 12f;
                float d = i < 12 ? distance : distance * 0.6f;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d;
                if (p.x < _mapMin.x || p.x > _mapMax.x || p.z < _mapMin.y || p.z > _mapMax.y)
                    continue;
                if (!Physics.Raycast(p + Vector3.up * DROP_HEIGHT, Vector3.down, out RaycastHit ground, DROP_HEIGHT * 2f,
                        ~0, QueryTriggerInteraction.Ignore))
                    continue;
                at = ground.point + Vector3.up * 0.6f;
                found = true;
            }

            if (!found)
            {
                Debug.LogWarning("EnemyTankSpawner: no ground round the player to put a tank on.");
                return null;
            }

            Vector3 look = randomSide ? center - at : (player != null ? player.forward : Vector3.forward);
            look.y = 0f;
            MonoEntity tank = Instantiate(_prefab, at, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            tank.name = name;
            return tank.gameObject.AddComponent<T>();
        }

        private void OnGUI()
        {
            int enemies = EnemyTankBrain.Alive;
            int allies = AllyTankBrain.Alive;
            if (enemies != _shown || allies != _shownAllies)
            {
                _shown = enemies;
                _shownAllies = allies;
                _text = "Танки: враги " + enemies + ", свои " + allies;
            }

            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            float x = Screen.width - WIDTH - MARGIN;
            float y = MARGIN + 60f + HEIGHT + 6f;
            GUI.Box(new Rect(x, y, WIDTH, HEIGHT), _text, _style);

            _buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(x, y + HEIGHT + 6f, WIDTH, HEIGHT), "+ Вражеский танк (J)", _buttonStyle))
                Spawn();
            if (GUI.Button(new Rect(x, y + 2f * (HEIGHT + 6f), WIDTH, HEIGHT), "+ Свой танк (K)", _buttonStyle))
                SpawnAlly();
            if (GUI.Button(new Rect(x, y + 3f * (HEIGHT + 6f), WIDTH, HEIGHT), "+ Червь (U)", _buttonStyle))
                WormPrototype.Spawn();
        }
    }
}
