using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class UniversalDebugger : MonoBehaviour
{
    [SerializeField] private MonoBehaviour _targetComponent;

    private DebuggerView _debuggerView;
    private Dictionary<string, object> _fieldValues;

    private void OnEnable()
    {
        if (_targetComponent == null)
            _targetComponent = GetComponent<MonoBehaviour>();

        _debuggerView = new DebuggerView(this);
        _fieldValues = new Dictionary<string, object>();
    }

    private void Update()
    {
        UpdateFieldValues();
        _debuggerView.Update();
    }

    private void OnGUI()
    {
        _debuggerView.OnGUI();
    }

    private void UpdateFieldValues()
    {
        if (_targetComponent == null)
            return;

        _fieldValues.Clear();
        Type type = _targetComponent.GetType();

        BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        FieldInfo[] fields = type.GetFields(flags);

        foreach (FieldInfo field in fields)
        {
            if (!field.IsPrivate || field.Name.StartsWith("m_") || field.Name.StartsWith("<"))
                continue;

            try
            {
                object value = field.GetValue(_targetComponent);
                string displayName = field.Name.StartsWith("_") ? field.Name.Substring(1) : field.Name;
                _fieldValues[displayName] = value;
            }
            catch
            {
                string displayName = field.Name.StartsWith("_") ? field.Name.Substring(1) : field.Name;
                _fieldValues[displayName] = null;
            }
        }
    }

    private class DebuggerView
    {
        private UniversalDebugger _parent;
        private bool _isVisible = true;
        private Vector2 _scrollPosition;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _fieldNameStyle;
        private bool _stylesInitialized = false;
        private readonly float _areaWidth = 400f; // ширина GUI

        public DebuggerView(UniversalDebugger parent)
        {
            _parent = parent;
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized)
                return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.green }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                normal = { textColor = Color.white }
            };

            _fieldNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.cyan }
            };

            _stylesInitialized = true;
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                _isVisible = !_isVisible;
        }

        public void OnGUI()
        {
            if (!_isVisible || _parent._targetComponent == null)
                return;

            InitializeStyles();

            // Располагаем GUI справа
            float xPos = Screen.width - _areaWidth - 10f; // 10 пикселей от правого края
            GUILayout.BeginArea(new Rect(xPos, 10, _areaWidth, Screen.height - 20));

            GUILayout.Label($"Component: {_parent._targetComponent.GetType().Name}", _fieldNameStyle);
            GUILayout.Space(10);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            DrawAllFields();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawAllFields()
        {
            if (_parent._fieldValues.Count == 0)
            {
                GUILayout.Label("No fields to display", _labelStyle);
                return;
            }

            foreach (var kvp in _parent._fieldValues)
            {
                DrawFieldWithValue(kvp.Key, kvp.Value);
            }
        }

        private void DrawFieldWithValue(string fieldName, object value)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label($"{fieldName}:", _fieldNameStyle, GUILayout.Width(200));
            GUILayout.FlexibleSpace();

            GUIStyle valueStyle;
            string displayValue;

            if (value == null)
            {
                valueStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    normal = { textColor = Color.red }
                };
                displayValue = "null";
            }
            else
            {
                valueStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    normal = { textColor = Color.green }
                };
                displayValue = FormatValue(value);
            }

            GUILayout.Label(displayValue, valueStyle, GUILayout.ExpandWidth(false));

            GUILayout.EndHorizontal();
        }

        private string FormatValue(object value)
        {
            if (value == null)
                return "null";

            if (value is bool boolVal)
                return boolVal ? "TRUE" : "FALSE";

            if (value is float floatVal)
                return floatVal.ToString("F2");

            if (value is double doubleVal)
                return doubleVal.ToString("F2");

            if (value is int intVal)
                return intVal.ToString();

            if (value is string str)
                return $"\"{str}\"";

            if (value is Vector3 vec3)
                return $"({vec3.x:F2}, {vec3.y:F2}, {vec3.z:F2})";

            if (value is Vector2 vec2)
                return $"({vec2.x:F2}, {vec2.y:F2})";

            if (value is Quaternion quat)
                return $"({quat.x:F2}, {quat.y:F2}, {quat.z:F2}, {quat.w:F2})";

            if (value is Array arr)
                return $"Array[{arr.Length}]";

            return value.ToString();
        }
    }
}
