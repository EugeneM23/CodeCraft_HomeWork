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
        private const float DROP_HEIGHT = 60f;
        private const float WIDTH = 220f;
        private const float HEIGHT = 34f;
        private const float MARGIN = 12f;

        private MonoEntity _prefab;
        private readonly PlayerTank _player = new();
        private GUIStyle _style;
        private int _shown = -1;
        private string _text = string.Empty;

        private Vector2 _mapMin = new(-40f, -40f);
        private Vector2 _mapMax = new(40f, 40f);

        public void Init(MonoEntity prefab, Vector2 mapMin, Vector2 mapMax)
        {
            _prefab = prefab;
            _mapMin = mapMin + Vector2.one * 4f;
            _mapMax = mapMax - Vector2.one * 4f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.jKey.wasPressedThisFrame)
                Spawn();
        }

        public EnemyTankBrain Spawn()
        {
            if (_prefab == null)
            {
                Debug.LogWarning("EnemyTankSpawner: no enemyTank in SandboxHordeConfig.");
                return null;
            }

            _player.Refresh();
            Transform player = _player.Transform;
            Vector3 center = player != null ? player.position : Vector3.zero;
            // A random side, inside the map and over ground: off the map's edge it fell through the void.
            Vector3 at = default;
            bool found = false;
            float start = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 24 && !found; i++)
            {
                float a = start + i * Mathf.PI * 2f / 12f;
                float distance = i < 12 ? DISTANCE : DISTANCE * 0.6f;
                Vector3 p = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * distance;
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

            Vector3 look = center - at;
            look.y = 0f;
            MonoEntity tank = Instantiate(_prefab, at, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
            tank.name = "[EnemyTank]";
            return tank.gameObject.AddComponent<EnemyTankBrain>();
        }

        private void OnGUI()
        {
            int alive = EnemyTankBrain.Alive;
            if (alive != _shown)
            {
                _shown = alive;
                _text = "Танки: " + alive + "  (J)";
            }

            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.Box(new Rect(Screen.width - WIDTH - MARGIN, MARGIN + 60f + HEIGHT + 6f, WIDTH, HEIGHT), _text, _style);
        }
    }
}
