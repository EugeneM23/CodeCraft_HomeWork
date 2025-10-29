using System;
using System.Text;
using UnityEngine;

namespace Game.Scripts.PlayerController
{
    public class PlayerControllerDebugger : MonoBehaviour
    {
        [SerializeField] private PlayerController _playerController;
        [SerializeField] private bool _showDebug = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;
        
        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;
        private bool _initialized;
        private Vector2 _scrollPosition;
        private readonly StringBuilder _stringBuilder = new StringBuilder();

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
            
            _labelStyle = new GUIStyle();
            _labelStyle.fontSize = 14;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.padding = new RectOffset(5, 5, 2, 2);

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = MakeTexture(2, 2, new Color(0, 0, 0, 0.7f));
            
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
            
            GUILayout.BeginArea(new Rect(windowRect.x + 5, windowRect.y + 5, windowRect.width - 10, windowRect.height - 10));
            
            DrawHeader();
            
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Width(width - 20), GUILayout.Height(height - 60));
            
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
            DrawBoolPropertySafe("IsGrabbingLedge", () => _playerController.IsGrabbingLedge);
            
            GUILayout.Space(10);
            
            // Vector2 Properties
            DrawSection("VECTORS");
            DrawVector2PropertySafe("Velocity", () => _playerController.Velocity);
            DrawVector2PropertySafe("MoveDirection", () => _playerController.MoveDirection);
            DrawVector2PropertySafe("SurfaceNormal", () => _playerController.SurfaceNormal);
            
            GUILayout.Space(10);
            
            // Component References
            DrawSection("COMPONENTS");
            DrawObjectPropertySafe("Collider", () => _playerController.Collider);
            DrawObjectPropertySafe("Stats", () => _playerController.Stats);
        }

        private void DrawSection(string sectionName)
        {
            GUILayout.Label(sectionName, GetStyle(new Color(0.5f, 0.8f, 1f), 14, FontStyle.Bold));
            DrawSeparator();
        }

        private void DrawBoolProperty(string name, bool value)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(name);
            _stringBuilder.Append(": ");
            _stringBuilder.Append(value);
            
            Color color = value ? Color.green : new Color(1f, 0.84f, 0f); // Gold color
            GUILayout.Label(_stringBuilder.ToString(), GetStyle(color, 14));
        }

        private void DrawBoolPropertySafe(string name, Func<bool> valueGetter)
        {
            try
            {
                DrawBoolProperty(name, valueGetter());
            }
            catch (Exception)
            {
                DrawErrorProperty(name);
            }
        }

        private void DrawVector2PropertySafe(string name, Func<Vector2> valueGetter)
        {
            try
            {
                DrawVector2Property(name, valueGetter());
            }
            catch (Exception)
            {
                DrawErrorProperty(name);
            }
        }

        private void DrawObjectPropertySafe(string name, Func<object> valueGetter)
        {
            try
            {
                DrawObjectProperty(name, valueGetter());
            }
            catch (Exception)
            {
                DrawErrorProperty(name);
            }
        }

        private void DrawErrorProperty(string name)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(name);
            _stringBuilder.Append(": N/A");
            
            GUILayout.Label(_stringBuilder.ToString(), GetStyle(Color.red, 14));
        }

        private void DrawVector2Property(string name, Vector2 value)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(name);
            _stringBuilder.Append(": (");
            _stringBuilder.Append(value.x.ToString("F2"));
            _stringBuilder.Append(", ");
            _stringBuilder.Append(value.y.ToString("F2"));
            _stringBuilder.Append(")");
            
            // Check if both components are zero
            Color color = (Mathf.Approximately(value.x, 0f) && Mathf.Approximately(value.y, 0f)) 
                ? Color.white 
                : new Color(1f, 0.84f, 0f);
            
            GUILayout.Label(_stringBuilder.ToString(), GetStyle(color, 14));
        }

        private void DrawObjectProperty(string name, object value)
        {
            _stringBuilder.Clear();
            _stringBuilder.Append(name);
            _stringBuilder.Append(": ");
            _stringBuilder.Append(value != null ? value.GetType().Name : "null");
            
            Color color = value != null ? new Color(1f, 0.84f, 0f) : Color.white;
            GUILayout.Label(_stringBuilder.ToString(), GetStyle(color, 14));
        }

        private void DrawSeparator()
        {
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));
        }

        private GUIStyle GetStyle(Color color, int fontSize, FontStyle fontStyle = FontStyle.Normal)
        {
            GUIStyle style = new GUIStyle(_labelStyle);
            style.normal.textColor = color;
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
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
    }
}