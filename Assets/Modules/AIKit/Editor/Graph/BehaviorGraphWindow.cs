using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Editor
{
    /// <summary>
    /// The behavior graph window: one canvas with the graph, nothing else. Opened with the button on a character's
    /// AICharacter (or Window → AI Kit → Behavior Graph, or "Open" on a graph asset); it follows the selected
    /// character. Nodes are added, wired and edited right on the canvas. In Play Mode the character's run is lit:
    /// the running node pulses, the edge it has just taken flashes.
    /// </summary>
    public sealed class BehaviorGraphWindow : EditorWindow
    {
        [SerializeField] private BehaviorGraphAsset _asset;
        [SerializeField] private AICharacter _character;
        [SerializeField] private string _characterId = "";

        private GraphDocument _document;
        private IGraphCanvas _canvas;
        private Label _hint;
        private AICharacter _agent;
        private BehaviorGraphInstance _watched;     // the run whose node changes are checked for breakpoints

        [MenuItem("Window/AI Kit/Behavior Graph")]
        public static void Open()
        {
            BehaviorGraphWindow window = GetWindow<BehaviorGraphWindow>("Behavior Graph");
            if (window._asset == null)
                window.OnSelectionChange();
        }

        public static void Open(BehaviorGraphAsset asset)
        {
            BehaviorGraphWindow window = GetWindow<BehaviorGraphWindow>("Behavior Graph");
            window.Load(asset);
        }

        /// <summary>Opens the graph of a character: its scene objects show on the nodes, in Play Mode its run is lit.</summary>
        public static void Open(AICharacter character)
        {
            BehaviorGraphWindow window = GetWindow<BehaviorGraphWindow>("Behavior Graph");
            window.SetCharacter(character);
            window._canvas.Element.schedule.Execute(window._canvas.Frame).ExecuteLater(50);
        }

        private void OnEnable()
        {
            rootVisualElement.Clear();
            var canvas = new GraphViewCanvas();
            _canvas = canvas;
            ElementInspector.Character = _character;
            rootVisualElement.Add(new IMGUIContainer(DrawToolbar));
            rootVisualElement.Add(canvas);

            _hint = new Label("Select a character with an AI Character component");
            _hint.style.position = Position.Absolute;
            _hint.style.left = _hint.style.right = 0;
            _hint.style.top = 60;
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hint.style.fontSize = 14;
            _hint.pickingMode = PickingMode.Ignore;
            rootVisualElement.Add(_hint);

            Undo.undoRedoPerformed += OnUndo;
            EditorApplication.update += OnUpdate;
            if (_asset != null)
                Load(_asset);
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo;
            EditorApplication.update -= OnUpdate;
            ElementInspector.Agent = null;
            Explain(null);
            Watch(null);
        }

        private void Load(BehaviorGraphAsset asset)
        {
            _asset = asset;
            _hint.style.display = asset == null ? DisplayStyle.Flex : DisplayStyle.None;
            if (asset == null)
                return;

            _document = new GraphDocument(asset);
            _document.Changed += () =>
            {
                _canvas.Show(_document);
                Repaint();
            };
            _canvas.Show(_document);
            _canvas.Frame();
        }

        private void SetCharacter(AICharacter character)
        {
            _character = character;
            _characterId = character != null ? GlobalObjectId.GetGlobalObjectIdSlow(character).ToString() : "";
            ElementInspector.Character = character;
            _agent = null;
            if (character != null && character.GraphAsset != null && character.GraphAsset != _asset)
                Load(character.GraphAsset);
            else if (_document != null)
                _canvas.Show(_document);
            Repaint();
        }

        private void OnSelectionChange()
        {
            AICharacter character = CharacterOf(Selection.activeGameObject);
            if (character != null && character != _character && character.GraphAsset != null)
                SetCharacter(character);
        }

        /// <summary>
        /// The character a selected object belongs to (null — none): the window follows it. The character on the object or
        /// above it (a bone, a weapon of it); else the one character under it (the root of a character whose component sits
        /// lower). An object beside characters, or a group of several, belongs to none of them.
        /// </summary>
        public static AICharacter CharacterOf(GameObject selected)
        {
            if (selected == null)
                return null;

            AICharacter own = selected.GetComponentInParent<AICharacter>(true);
            if (own != null)
                return own;

            AICharacter[] under = selected.GetComponentsInChildren<AICharacter>(true);
            return under.Length == 1 ? under[0] : null;
        }

        /// <summary>
        /// The character runs the graph of this asset (the window shows its run): it was given this asset, or the graph it
        /// runs is the one compiled from it. Asked in that order: the asset is compiled only for a character of another
        /// one (a compile logs every problem of a graph being edited, once per edit).
        /// </summary>
        public static bool RunsThisGraph(BehaviorGraph running, BehaviorGraphAsset runningAsset, BehaviorGraphAsset shown) =>
            running != null && shown != null && (runningAsset == shown || running == shown.Compile());

        private void OnUndo()
        {
            if (_document == null)
                return;

            _asset.MarkChanged();
            _document.Reload();
            _canvas.Show(_document);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(Title(), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (_document != null)
                {
                    int errors = _document.Issues.Count(issue => issue.IsError);
                    int warnings = _document.Issues.Count - errors + AICharacterEditor.Problems(_character).Count;
                    if (errors + warnings > 0)
                    {
                        string text = errors > 0 && warnings > 0 ? $"\u26a0 {errors} errors, {warnings} warnings"
                            : errors > 0 ? $"\u26a0 {errors} errors" : $"\u26a0 {warnings} warnings";
                        if (GUILayout.Button(new GUIContent(text, "Click: go to a node with a problem"), EditorStyles.toolbarDropDown))
                            ShowIssues();
                    }

                    if (GUILayout.Button("Frame", EditorStyles.toolbarButton))
                        _canvas.Frame();
                }
            }
        }

        private string Title()
        {
            if (_asset == null)
                return "";

            string title = _character != null ? $"{_character.name}  —  {_asset.name}" : _asset.name;
            if (!Application.isPlaying || _agent == null || _agent.State == null)
                return title;

            return $"{title}  —  running: {_agent.State.DisplayName}";
        }

        // The problems of the graph, errors first: a pick brings its node into view
        private void ShowIssues()
        {
            var menu = new GenericMenu();
            foreach (GraphIssue issue in _document.Issues.OrderBy(issue => issue.IsError ? 0 : 1))
            {
                string label = $"{(issue.IsError ? "Error" : "Warning")}: {issue.Message}".Replace('/', '\u2215');
                int id = issue.RecordId;
                menu.AddItem(new GUIContent(label.Length > 120 ? label.Substring(0, 120) + "\u2026" : label), false,
                    () => _canvas.FocusNode(id));
            }

            // The character's own problems (no weapon, no such move or ability): there is no node to go to
            foreach (string problem in AICharacterEditor.Problems(_character))
                menu.AddDisabledItem(new GUIContent($"Character: {problem}".Replace('/', '∕')));

            menu.ShowAsContext();
        }

        // Play pauses when the watched character comes into a node with a breakpoint
        // The agent the window shows writes out why each decision does not hold (a string per think); the rest do not
        private AICharacter _explained;

        private void Explain(AICharacter agent)
        {
            if (_explained == agent)
                return;

            if (_explained != null && _explained.Context != null)
                _explained.Context.ExplainsDecisions = false;

            _explained = agent;
            if (agent != null && agent.Context != null)
                agent.Context.ExplainsDecisions = true;
        }

        private void Watch(BehaviorGraphInstance run)
        {
            if (_watched == run)
                return;

            if (_watched != null)
                _watched.StateChanged -= OnStateChanged;
            _watched = run;
            if (_watched != null)
                _watched.StateChanged += OnStateChanged;
        }

        private void OnStateChanged(AgentState from, AgentState to)
        {
            if (to == null || !GraphBreakpoints.Has(_asset, to.Id))
                return;

            EditorApplication.isPaused = true;
            ShowNotification(new GUIContent($"Breakpoint: {to.DisplayName}"), 3.0);
            _canvas.FocusNode(to.Id);
        }

        private void OnUpdate()
        {
            if (_character == null && !string.IsNullOrEmpty(_characterId) &&
                GlobalObjectId.TryParse(_characterId, out GlobalObjectId id))
            {
                // The scene was reloaded (Play Mode): the same component is a new object now
                var found = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as AICharacter;
                if (found != null)
                    SetCharacter(found);
            }

            ElementInspector.Character = _character;
            if (_document == null)
                return;

            if (!Application.isPlaying)
            {
                ElementInspector.Agent = null;
                if (_agent != null)
                {
                    _agent = null;
                    Watch(null);
                    _canvas.Highlight(null, 0, 0f, null, 0f);
                    _canvas.HighlightSensors(null);
                    Repaint();
                }

                return;
            }

            if (_agent == null && _character != null)
                _agent = _character;

            if (_agent == null && Selection.activeGameObject != null)
                _agent = CharacterOf(Selection.activeGameObject);

            // The agent runs this graph: compiled from this asset — also when the asset was edited after the agent
            // started (it keeps the graph it started with; its nodes are found by id, so the window still follows it)
            if (_agent == null || _agent.Graph == null || !RunsThisGraph(_agent.Graph.Graph, _agent.GraphAsset, _asset))
            {
                _agent = null;                          // runs another graph: nothing of it to show here
                ElementInspector.Agent = null;
                Explain(null);
                Watch(null);
                return;
            }

            ElementInspector.Agent = _agent;
            Explain(_agent);
            Watch(_agent.Graph);
            _agent.IsTracing = true;                    // the last edge comes from the decision trace
            AgentContext context = _agent.Context;
            AgentState leaf = _agent.Graph.Current;
            var active = new HashSet<int>(_agent.Graph.ActivePath.Select(state => state.Id));
            Transition last = null;
            float stamp = 0f;
            DecisionTrace trace = context.Trace;
            for (int i = trace.Count - 1; i >= 0; i--)
            {
                DecisionRecord record = trace[i];
                if (record.Kind != DecisionKind.Transition)
                    continue;                           // ability records: look for the last move between states

                last = record.Transition;
                stamp = record.Time;
                break;
            }

            _canvas.Highlight(active, leaf?.Id ?? 0, leaf != null ? leaf.TimeIn(context) : 0f, last, stamp);
            _canvas.HighlightSensors(context.Blackboard);
            Repaint();
        }
    }
}
