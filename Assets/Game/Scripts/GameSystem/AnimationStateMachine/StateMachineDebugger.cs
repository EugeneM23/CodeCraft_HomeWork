using System;
using System.Text;
using Gameplay;
using UnityEngine;

internal class StateMachineDebugger : MonoBehaviour
{
    [Inject] private AnimationFSM _animationFsm;
    [Inject] private SpriteAnimator _animator;
    [SerializeField] private bool _showDebug = true;
    [SerializeField] private KeyCode _toggleKey = KeyCode.F4;

    private GUIStyle _labelStyle;
    private GUIStyle _boxStyle;
    private bool _initialized;
    private readonly StringBuilder _stringBuilder = new();

    private Type _previousStateType;
    private Type _currentStateType;
    private string _currentAnimationName = "None";

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey))
        {
            _showDebug = !_showDebug;
        }

        UpdateStateTracking();
        UpdateAnimationTracking();
    }

    private void UpdateStateTracking()
    {
        if (_animationFsm == null) return;

        try
        {
            var currentStateField = typeof(AnimationFSM).GetField("_currentState",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (currentStateField != null)
            {
                var currentState = currentStateField.GetValue(_animationFsm);
                if (currentState != null)
                {
                    Type newStateType = currentState.GetType();

                    if (_currentStateType != newStateType)
                    {
                        _previousStateType = _currentStateType;
                        _currentStateType = newStateType;
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void UpdateAnimationTracking()
    {
        if (_animator == null) return;

        try
        {
            var currentAnim = _animator.CurrentAnimation;
            if (currentAnim != null)
            {
                _currentAnimationName = currentAnim.ToString();
            }
        }
        catch
        {
            _currentAnimationName = "N/A";
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
        if (!_showDebug || _animationFsm == null) return;

        InitializeStyles();

        float width = 350f;
        float height = 250f;
        float x = Screen.width - width - 10f;
        float y = 10f;

        Rect windowRect = new Rect(x, y, width, height);

        GUI.Box(windowRect, "", _boxStyle);

        GUILayout.BeginArea(new Rect(windowRect.x + 5, windowRect.y + 5,
            windowRect.width - 10, windowRect.height - 10));

        DrawHeader();
        GUILayout.Space(10);
        DrawDebugInfo();

        GUILayout.EndArea();
    }

    private void DrawHeader()
    {
        _labelStyle.fontSize = 16;
        _labelStyle.fontStyle = FontStyle.Bold;
        _labelStyle.normal.textColor = Color.yellow;

        GUILayout.Label("State Machine Debug", _labelStyle);
        GUILayout.Label($"Press {_toggleKey} to toggle", GetStyle(Color.gray, 12));

        GUILayout.Space(5);
        DrawSeparator();
        GUILayout.Space(5);

        _labelStyle.fontSize = 14;
        _labelStyle.fontStyle = FontStyle.Normal;
    }

    private void DrawDebugInfo()
    {
        // Current State
        DrawSection("CURRENT STATE");
        DrawStateProperty("State", _currentStateType);

        GUILayout.Space(10);

        // Previous State
        DrawSection("PREVIOUS STATE");
        DrawStateProperty("State", _previousStateType);

        GUILayout.Space(10);

        // Animation
        DrawSection("ANIMATION");
        DrawAnimationProperty("Current", _currentAnimationName);

        if (_animator != null)
        {
            try
            {
                var anim = _animator.CurrentAnimation;
                if (anim != null)
                {
                    var canInterruptField = anim.GetType().GetProperty("CanInterrupt");
                    if (canInterruptField != null)
                    {
                        bool canInterrupt = (bool)canInterruptField.GetValue(anim);
                        DrawBoolProperty("CanInterrupt", canInterrupt);
                    }
                }
            }
            catch
            {
            }
        }
    }

    private void DrawSection(string sectionName)
    {
        GUILayout.Label(sectionName, GetStyle(new Color(1f, 0.5f, 0.8f), 14, FontStyle.Bold));
        DrawSeparator();
    }

    private void DrawStateProperty(string name, Type stateType)
    {
        _stringBuilder.Clear();
        _stringBuilder.Append(name).Append(": ");

        if (stateType != null)
        {
            string stateName = stateType.Name;
            _stringBuilder.Append(stateName);
            GUILayout.Label(_stringBuilder.ToString(), GetStyle(Color.green, 14));
        }
        else
        {
            _stringBuilder.Append("None");
            GUILayout.Label(_stringBuilder.ToString(), GetStyle(Color.gray, 14));
        }
    }

    private void DrawAnimationProperty(string name, string animationName)
    {
        _stringBuilder.Clear();
        _stringBuilder.Append(name).Append(": ").Append(animationName);

        Color color = animationName != "None" && animationName != "N/A"
            ? new Color(0.5f, 1f, 0.8f)
            : Color.gray;

        GUILayout.Label(_stringBuilder.ToString(), GetStyle(color, 14));
    }

    private void DrawBoolProperty(string name, bool value)
    {
        _stringBuilder.Clear();
        _stringBuilder.Append(name).Append(": ").Append(value);

        Color color = value ? Color.green : new Color(1f, 0.84f, 0f);
        GUILayout.Label(_stringBuilder.ToString(), GetStyle(color, 14));
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
        if (_animationFsm == null || _currentStateType == null) return;

        // Визуализация текущего состояния
        Vector3 debugPos = transform.position + Vector3.up * 2.5f;
        UnityEditor.Handles.color = Color.green;
        UnityEditor.Handles.Label(debugPos, $"State: {_currentStateType.Name}");
    }
#endif
}