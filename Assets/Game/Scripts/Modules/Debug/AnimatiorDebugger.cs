using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    public class AnimationDebugger : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private string ControllerName => _animator.runtimeAnimatorController.name;
        private string _velocity;
        private string[] _parameter;
        private string _currentAnimation;

        private Dictionary<AnimatorControllerParameterType, Func<string, object>> _getterMap;
        private Vector3 _lastPosition;

        private DebuggerView _debuggerView;

        private void Awake()
        {
            _getterMap = new Dictionary<AnimatorControllerParameterType, Func<string, object>>
            {
                { AnimatorControllerParameterType.Float, name => _animator.GetFloat(name) },
                { AnimatorControllerParameterType.Int, name => _animator.GetInteger(name) },
                { AnimatorControllerParameterType.Bool, name => _animator.GetBool(name) },
                { AnimatorControllerParameterType.Trigger, name => "(Trigger)" }
            };

            _debuggerView = new DebuggerView(this);
        }

        private void Update()
        {
            GetAllparametersInfo();
            GetCurrentAnimationInfo();
            GetVelocity();

            _debuggerView.Update();
        }

        private void OnGUI()
        {
            _debuggerView.OnGUI();
        }

        private void GetAllparametersInfo()
        {
            AnimatorControllerParameter[] parameters = _animator.parameters;
            string[] parameterNames = new string[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                object value = _getterMap[parameters[i].type](parameters[i].name);
                parameterNames[i] = $"{parameters[i].name}  --->  {value}";
            }

            _parameter = parameterNames;
        }

        private void GetCurrentAnimationInfo()
        {
            AnimatorClipInfo[] clipInfo = _animator.GetCurrentAnimatorClipInfo(0);
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

            if (clipInfo.Length > 0)
                _currentAnimation = $"{clipInfo[0].clip.name} : Speed - {stateInfo.speed}";
        }

        private void GetVelocity()
        {
            int speed = Convert.ToInt32((transform.position - _lastPosition).magnitude / Time.deltaTime);
            _velocity = speed.ToString();
            _lastPosition = transform.position;
        }

        // Внутренний класс для отображения данных
        private class DebuggerView
        {
            private AnimationDebugger _parent;
            private bool _isVisible = true;
            private Vector2 _scrollPosition;
            private GUIStyle _headerStyle;
            private GUIStyle _labelStyle;
            private GUIStyle _boxStyle;
            private bool _stylesInitialized = false;

            public DebuggerView(AnimationDebugger parent)
            {
                _parent = parent;
            }

            private void InitializeStyles()
            {
                if (_stylesInitialized)
                    return;

                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.green }
                };

                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    normal = { textColor = Color.white }
                };

                _boxStyle = new GUIStyle(GUI.skin.box)
                {
                    padding = new RectOffset(10, 10, 10, 10),
                    normal = { background = Texture2D.whiteTexture }
                };
                _boxStyle.normal.background.name = "Clear";

                _stylesInitialized = true;
            }

            public void Update()
            {
                if (Input.GetKeyDown(KeyCode.F1))
                    _isVisible = !_isVisible;
            }

            public void OnGUI()
            {
                if (!_isVisible)
                    return;

                InitializeStyles();

                GUILayout.BeginArea(new Rect(10, 10, 600, Screen.height - 20));
                GUILayout.Label("🎬 ANIMATION DEBUGGER", _headerStyle);
                GUILayout.Label("(Press F1)", _labelStyle);
                GUILayout.Space(10);

                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

                DrawField("Controller Name", _parent.ControllerName);
                DrawField("Current Animation", _parent._currentAnimation);
                DrawField("Velocity", _parent._velocity + " units/sec");

                GUILayout.Space(10);
                GUILayout.Label("Parameters:", _headerStyle);
                DrawParametersWithColors();

                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }

            private void DrawField(string name, string value)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>{name}:</b>", _labelStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(value, _labelStyle);
                GUILayout.EndHorizontal();
            }

            private void DrawParametersWithColors()
            {
                if (_parent._parameter == null || _parent._parameter.Length == 0)
                {
                    GUILayout.Label("No parameters", _labelStyle);
                    return;
                }

                foreach (string param in _parent._parameter)
                {
                    GUIStyle paramStyle = GetParameterStyle(param);
                    GUILayout.Label(param, paramStyle);
                }
            }

            private GUIStyle GetParameterStyle(string paramText)
            {
                GUIStyle style = new GUIStyle(_labelStyle);

                if (paramText.Contains("True"))
                    style.normal.textColor = Color.green;
                else if (paramText.Contains("False"))
                    style.normal.textColor = Color.red;

                return style;
            }
        }
    }
}