using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>One scene object given to a Transform variable of the graph.</summary>
    [Serializable]
    public sealed class ObjectBinding
    {
        public string Variable;
        public Transform Object;
    }

    /// <summary>
    /// The Behavior section of the character: the graph asset it runs, and how it is drawn in the Scene view (the graph's
    /// sensors, the node it is in). No scene objects live here (step 90, C18): the graph finds the objects of the world
    /// itself (Find, the registry of the world), its target comes from its sensors, and an object that is somebody's
    /// says so itself (its Owners). Marked [AIReadable] (step 91): a Try Get Component may look for an AI Character first in its list, and a
    /// sensor may look for AI Characters.
    /// </summary>
    [AIReadable]
    public sealed partial class AICharacter
    {
        [Header("Behavior")]
        [SerializeField] private BehaviorGraphAsset _graph;

        // Legacy (before step 90): scene objects the character gave the graph's Transform variables. Nothing reads it
        // at run time any more; only the scene migration (Tools → AI Kit → Migrate Scene Objects To AI Objects) reads it,
        // turns each into an AI Object owned by the character, and clears it
#pragma warning disable CS0414 // read by the migration through SerializedObject only
        [SerializeField, HideInInspector] private List<ObjectBinding> _bindings = new();
#pragma warning restore CS0414

        [Tooltip("Draw the graph's sensors around the character in the Scene view (and in the Game view with Gizmos on)")]
        [SerializeField] private bool _drawSensors = true;

        [Tooltip("In Play: the name of the node the character is in, written above its head in the Scene view (Gizmos on)")]
        [SerializeField] private bool _showState = true;

        [Tooltip("How high above the character's feet the name is written")]
        [SerializeField] private float _stateHeight = 2.4f;

        [Tooltip("Write the character's name before the node's: for a scene with many characters, each with a graph of its own")]
        [SerializeField] private bool _showName;

        /// <summary>The graph asset this character runs (Graph is the running copy).</summary>
        public BehaviorGraphAsset GraphAsset
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>
        /// The character and the objects of the world: whatever it went for is let go when its decision in charge leaves
        /// (another decision, none, the graph stopped — death included): a claim is for the chain that made it.
        /// </summary>
        private void InitializeObjects()
        {
            if (Context != null)
                Context.DecisionLeft += ReleaseClaims;
        }

        private void ReleaseClaims() => Context?.WorldObjects.ReleaseAll(this);

        // ---------- debug drawing: the current node above the head (Scene view, Play) ----------

#if UNITY_EDITOR
        private static GUIStyle _stateStyle;

        private void DrawState()
        {
            if (!_showState || !Application.isPlaying)
                return;

            AgentState state = State;
            if (state == null)
                return;

            _stateStyle ??= new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            string text = _showName ? $"{name}: {state.DisplayName}" : state.DisplayName;
            UnityEditor.Handles.Label(transform.position + Vector3.up * _stateHeight, text, _stateStyle);
        }
#endif

        // ---------- debug drawing: the graph's sensors in the Scene view ----------

        private static readonly Color SensorColor = new(0.3f, 0.8f, 1f);
        private static readonly Color SeeingColor = new(1f, 0.8f, 0.2f);

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            DrawState();
#endif
            if (!_drawSensors || _graph == null)
                return;

            BehaviorGraph graph = _graph.Peek();
            if (graph == null)
                return;

            // The body the sensors look from: the agent's entity in Play Mode, this object before
            Transform body = Context != null && Context.Self != null ? Context.Self : transform;
            Vector3 origin = Context != null ? Context.Position : body.position;
            origin += Vector3.up * 0.05f;               // just above the ground: not hidden in it
            List<AgentTask> tasks = graph.Root.Tasks;
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i] is not SensorTask sensor)
                    continue;

                Transform seen = Application.isPlaying ? sensor.SeenBy(Context) : null;
                Gizmos.color = seen != null ? SeeingColor : SensorColor;
                sensor.DrawGizmos(origin, body.forward, Application.isPlaying ? Context : null, ObjectOf);
                if (seen != null)
                {
                    Gizmos.DrawLine(origin, seen.position);
                    Gizmos.DrawWireSphere(seen.position, 0.35f);
                }
            }
        }

        // A Transform variable's object: what the running agent has (none before Play: objects are the graph's to find)
        private Transform ObjectOf(string variable) =>
            Application.isPlaying && Context != null ? Context.Blackboard.Get<Transform>(variable) : null;
    }
}
