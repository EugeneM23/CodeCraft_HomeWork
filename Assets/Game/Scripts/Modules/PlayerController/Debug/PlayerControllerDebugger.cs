using System;
using System.Text;
using UnityEngine;

namespace Game.Scripts.Modules.PlayerController.Debug
{
    internal class PlayerControllerDebugger : MonoBehaviour
    {
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private bool _showDebug = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;

        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;
        private bool _initialized;
        private Vector2 _scrollPosition;
        private readonly StringBuilder _stringBuilder = new();

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _showDebug = !_showDebug;
            }
        }

        private void InitializeStyles()
        {
            if (_initialized) return;

            _labelStyle = new GUIStyle
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                padding = new RectOffset(5, 5, 2, 2)
            };

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, new Color(0, 0, 0, 0.7f)) }
            };

            _initialized = true;
        }

        private void OnGUI()
        {
            if (!_showDebug || _playerController == null) return;

            InitializeStyles();

            float width = 350f;
            float height = Screen.height - 40f;
            Rect windowRect = new Rect(10, 10, width, height);

            GUI.Box(windowRect, "", _boxStyle);

            GUILayout.BeginArea(new Rect(windowRect.x + 5, windowRect.y + 5, windowRect.width - 10,
                windowRect.height - 10));

            DrawHeader();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(width - 20),
                GUILayout.Height(height - 60));

            DrawDebugInfo();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            _labelStyle.fontSize = 16;
            _labelStyle.fontStyle = FontStyle.Bold;
            _labelStyle.normal.textColor = Color.yellow;

            GUILayout.Label("PlayerController Debug", _labelStyle);
            GUILayout.Label($"Press {_toggleKey} to toggle", GetStyle(Color.gray, 12));

            GUILayout.Space(5);
            DrawSeparator();
            GUILayout.Space(5);

            _labelStyle.fontSize = 14;
            _labelStyle.fontStyle = FontStyle.Normal;
        }

        private void DrawDebugInfo()
        {
            // Boolean Properties
            DrawSection("STATE");
            DrawBoolPropertySafe("IsGrounded", () => _playerController.IsGrounded);
            DrawBoolPropertySafe("IsCeilingHit", () => _playerController.IsCeilingHit);
            DrawBoolPropertySafe("IsOnStairs", () => _playerController.IsOnStairs);
            DrawBoolPropertySafe("IsOnSlope", () => _playerController.IsOnSlope);
            DrawBoolPropertySafe("IsOnWall", () => _playerController.IsOnWall);
            DrawBoolPropertySafe("IsOnWallSliding", () => _playerController.IsOnWallSliding);
            DrawBoolPropertySafe("IsGrabbingLedge", () => _playerController.IsGrabbingLedge);
            DrawBoolPropertySafe("IsSlidingOnslope", () => _playerController.IsSlidingOnslope);

            GUILayout.Space(10);

            // Vector2 Properties
            DrawSection("VECTORS");
            DrawVector2PropertySafe("Velocity", () => _playerController.Velocity);
            DrawVector2PropertySafe("MoveDirection", () => _playerController.MoveDirection);
            DrawVector2PropertySafe("SurfaceNormal", () => _playerController.SurfaceNormal);

            GUILayout.Space(10);

            // Numeric Properties
            DrawSection("NUMERIC");
            DrawFloatPropertySafe("DistanceToGround", () => _playerController.DistanceToGround);
            DrawIntPropertySafe("WallDirection", () => _playerController.WallDirection);

            GUILayout.Space(10);

            // Component References
            DrawSection("COMPONENTS");
            DrawObjectPropertySafe("Rigidbody", () => _playerController.GetComponent<Rigidbody2D>());
            DrawObjectPropertySafe("Collider", () => _playerController.Collider);
            DrawObjectPropertySafe("Stats", () => _playerController.Stats);

            GUILayout.Space(10);

            // Internals / Systems
            DrawSection("SYSTEMS");
            DrawObjectPropertySafe("ServiceLocator",
                () => typeof(PlayerController)
                    .GetField("_locator",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_playerController));
            DrawObjectPropertySafe("Tickables",
                () => typeof(PlayerController)
                    .GetField("_tickables",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_playerController));
            DrawObjectPropertySafe("Velocities",
                () => typeof(PlayerController)
                    .GetField("_velocities",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(_playerController));
        }

        private void DrawSection(string sectionName)
        {
            GUILayout.Label(sectionName, GetStyle(new Color(0.5f, 0.8f, 1f), 14, FontStyle.Bold));
            DrawSeparator();
        }

        private void DrawBoolProperty(string name, bool value)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(name).Append(": ").Append(value);

            Color color = value ? Color.green : new Color(1f, 0.84f, 0f);
            GUILayout.Label(_stringBuilder.ToString(), GetStyle(color, 14));
        }

        private void DrawBoolPropertySafe(string name, Func<bool> getter)
        {
            try
            {
                DrawBoolProperty(name, getter());
            }
            catch
            {
                DrawErrorProperty(name);
            }
        }

        private void DrawVector2PropertySafe(string name, Func<Vector2> getter)
        {
            try
            {
                DrawVector2Property(name, getter());
            }
            catch
            {
                DrawErrorProperty(name);
            }
        }

        private void DrawObjectPropertySafe(string name, Func<object> getter)
        {
            try
            {
                DrawObjectProperty(name, getter());
            }
            catch
            {
                DrawErrorProperty(name);
            }
        }

        private void DrawFloatPropertySafe(string name, Func<float> getter)
        {
            try
            {
                float value = getter();
                _stringBuilder.Clear();
                _stringBuilder.Append(name).Append(": ").Append(value.ToString("F2"));
                GUILayout.Label(_stringBuilder.ToString(), GetStyle(Color.cyan, 14));
            }
            catch
            {
                DrawErrorProperty(name);
            }
        }

        private void DrawIntPropertySafe(string name, Func<int> getter)
        {
            try
            {
                int value = getter();
                _stringBuilder.Clear();
                _stringBuilder.Append(name).Append(": ").Append(value);
                GUILayout.Label(_stringBuilder.ToString(), GetStyle(Color.cyan, 14));
            }
            catch
            {
                DrawErrorProperty(name);
            }
        }

        private void DrawVector2Property(string name, Vector2 value)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(name).Append(": (")
                .Append(value.x.ToString("F2")).Append(", ")
                .Append(value.y.ToString("F2")).Append(")");

            Color color = (Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, 0f))
                ? Color.white
                : new Color(1f, 0.84f, 0f);

            GUILayout.Label(_stringBuilder.ToString(), GetStyle(color, 14));
        }

        private void DrawObjectProperty(string name, object value)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(name).Append(": ")
                .Append(value != null ? value.GetType().Name : "null");

            Color color = value != null ? new Color(1f, 0.84f, 0f) : Color.white;
            GUILayout.Label(_stringBuilder.ToString(), GetStyle(color, 14));
        }

        private void DrawErrorProperty(string name)
        {
            GUILayout.Label($"{name}: N/A", GetStyle(Color.red, 14));
        }

        private void DrawSeparator()
        {
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        private GUIStyle GetStyle(Color color, int fontSize, FontStyle fontStyle = FontStyle.Normal)
        {
            GUIStyle style = new GUIStyle(_labelStyle)
            {
                normal = { textColor = color },
                fontSize = fontSize,
                fontStyle = fontStyle
            };
            return style;
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_playerController == null) return;

            // Безопасно получаем все необходимые данные
            Vector2 moveDir = Vector2.zero;
            Vector2 velocity = Vector2.zero;
            Vector2 normal = Vector2.up;
            CapsuleCollider2D collider = null;

            try
            {
                moveDir = _playerController.MoveDirection;
                velocity = _playerController.Velocity;
                normal = _playerController.SurfaceNormal;
                collider = _playerController.Collider;
            }
            catch
            {
                return;
            }

            Vector3 playerPos = _playerController.transform.position;

            // === 1. INPUT DIRECTION ===
            Vector3 headPos = playerPos + Vector3.up * (collider != null ? collider.size.y / 2 + 0.5f : 1.5f);
            if (moveDir.sqrMagnitude > 0.001f)
            {
                DrawArrow(headPos, moveDir.normalized, Color.green, 1f);
            }

            // === 2. VELOCITY ===
            float velocityMagnitude = velocity.magnitude;
            if (velocityMagnitude > 0.001f)
            {
                // Длина стрелки = величина Velocity
                DrawArrow(playerPos, velocity.normalized / 10, Color.cyan, velocityMagnitude);
            }

            // === 3. SURFACE NORMAL ===
            if (normal.sqrMagnitude > 0.001f && _playerController.IsGrounded)
            {
                Vector3 groundPos = playerPos + Vector3.down * (collider != null ? collider.size.y / 2 + 0.1f : 0.6f);
                DrawArrow(groundPos, normal.normalized, Color.yellow, 0.8f);
            }
        }

        /// <summary>
        /// Универсальная функция для рисования стрелки Gizmos.
        /// </summary>
        private void DrawArrow(Vector3 start, Vector3 direction, Color color, float length)
        {
            Gizmos.color = color;
            Vector3 end = start + direction * length;
            Gizmos.DrawLine(start, end);

            // Наконечник стрелки
            Vector3 right = Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(0, 0, 150) * direction * 0.25f;
            Vector3 left = Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(0, 0, -150) * direction * 0.25f;
            Gizmos.DrawLine(end, end + right);
            Gizmos.DrawLine(end, end + left);
        }
#endif
    }
}