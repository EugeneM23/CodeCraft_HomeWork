using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AIKit
{
    /// <summary>
    /// Answers "why did it do that?" in the Scene view: above the agent — the active state path, the running tasks,
    /// the agent's blackboard and its last decisions (the trace is switched on while this component is enabled).
    /// Editor-only: in a build the component does nothing, so it may stay on prefabs.
    /// Put it next to the AICharacter (or anywhere under the same entity).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Kit/Agent Debug")]
    public sealed class AgentDebugComponent : MonoBehaviour
    {
        [Tooltip("Empty — the AICharacter of this entity")]
        [SerializeField] private AICharacter _agent;

        [Tooltip("Draw only while the object is selected")]
        [SerializeField] private bool _onlySelected = true;

        [Tooltip("How many of the last decisions to show")]
        [Range(0, 32)]
        [SerializeField] private int _decisions = 6;

        [Tooltip("Show the agent's own blackboard")]
        [SerializeField] private bool _blackboard = true;

        [Tooltip("Label height above the agent, world units")]
        [SerializeField] private float _height = 2.4f;

        [Tooltip("Navigation: the line to the steering point coloured by status, the current edge and status in the " +
                 "label, and the last refusals (Blocked / Unreachable) with where and when")]
        [SerializeField] private bool _navigation;

        [Tooltip("Combat: the phase of the ability that plays, who holds which axis of the body, the attack tokens of the " +
                 "target and who holds them")]
        [SerializeField] private bool _combat;

        [Tooltip("Perception: the cones, a ray to every perceived target with its awareness, the last known place, the " +
                 "alert level in the label")]
        [SerializeField] private bool _perception;

        [Tooltip("How many navigation refusals to keep")]
        [Range(1, 16)]
        [SerializeField] private int _refusals = 4;

        private readonly StringBuilder _text = new();
        private readonly System.Collections.Generic.List<string> _refusalLog = new();
        private NavStatus _lastStatus;

        /// <summary>Navigation drawn and labelled.</summary>
        /// <summary>Combat (phase, arbiter, tokens) labelled and drawn.</summary>
        public bool ShowCombat
        {
            get => _combat;
            set => _combat = value;
        }

        /// <summary>Perception drawn and labelled.</summary>
        public bool ShowPerception
        {
            get => _perception;
            set => _perception = value;
        }

        public bool ShowNavigation
        {
            get => _navigation;
            set => _navigation = value;
        }

        /// <summary>The kept refusals, oldest first: "t=1.20 Blocked Jump at (3.0, 0.0)".</summary>
        public System.Collections.Generic.IReadOnlyList<string> Refusals => _refusalLog;

        /// <summary>
        /// Notes a refusal when the navigator has just turned Blocked or Unreachable. Called every frame while the
        /// navigation view is on (and by tests directly).
        /// </summary>
        public void SampleNavigation()
        {
            INavigator navigation = _agent != null ? _agent.Context?.Navigation : null;
            if (navigation == null)
                return;

            NavStatus status = navigation.Status;
            bool refused = status == NavStatus.Blocked || status == NavStatus.Unreachable;
            if (refused && status != _lastStatus)
            {
                Vector3 at = _agent.Context.Position;
                _refusalLog.Add($"t={_agent.Context.LocalTime:0.00} {status} {navigation.CurrentEdge} at " +
                                $"({at.x:0.0}, {at.y:0.0})");
                while (_refusalLog.Count > _refusals)
                    _refusalLog.RemoveAt(0);
            }

            _lastStatus = status;
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (_navigation)
                SampleNavigation();
#endif
        }

        private void AppendCombat()
        {
            AgentContext context = _agent.Context;
            _text.Append("phase: ").Append(_agent.Abilities.RunningPhase).Append('\n');
            _text.Append("move: ").Append(context.Arbiter.AuthorityOf(BodyAxis.Move)).Append(' ')
                .Append(NameOf(context.Arbiter.OwnerOf(BodyAxis.Move))).Append(", face: ")
                .Append(context.Arbiter.AuthorityOf(BodyAxis.Face)).Append('\n');

            Engagement engagement = context.Engagement;
            if (engagement == null)
                return;

            foreach (string pool in engagement.Tokens.Pools)
            {
                int used = engagement.Tokens.Used(pool);
                if (used == 0 && !engagement.Tokens.Holds(_agent, pool))
                    continue;

                _text.Append("tokens ").Append(pool).Append(": ").Append(used).Append('/')
                    .Append(engagement.CapacityOf(pool)).Append(engagement.Tokens.Holds(_agent, pool) ? " (holds)" : "")
                    .Append('\n');
            }
        }

        private static string NameOf(object owner) => owner is AgentNode node ? node.DisplayName : owner?.ToString() ?? "-";

        private void AppendPerception()
        {
            AgentPerception perception = _agent.Context.Perception;
            if (perception == null)
            {
                _text.Append("senses: none (target set by the game)").Append('\n');
                return;
            }

            _text.Append("alert: ").Append(perception.Level).Append(", awareness ")
                .Append(perception.Awareness.ToString("0.00")).Append('\n');
        }

        private void AppendNavigation()
        {
            INavigator navigation = _agent.Context.Navigation;
            if (navigation == null)
            {
                _text.Append("nav: none\n");
                return;
            }

            _text.Append("nav: ").Append(navigation.Status).Append(", ").Append(navigation.CurrentEdge);
            if (navigation.Status.IsMoving())
                _text.Append(", ").Append(navigation.RemainingDistance.ToString("0.0")).Append(" m");
            _text.Append('\n');
            for (int i = 0; i < _refusalLog.Count; i++)
                _text.Append("  ").Append(_refusalLog[i]).Append('\n');
        }

        public AICharacter Agent => _agent;

        /// <summary>What the label shows now; empty when the agent has no context yet. Allocates — debugging only.</summary>
        public string Describe()
        {
            _text.Clear();
            if (_agent == null || _agent.Context == null)
                return string.Empty;

            AppendStates();
            if (_navigation)
                AppendNavigation();
            if (_perception)
                AppendPerception();
            if (_combat)
                AppendCombat();
            if (_blackboard)
                AppendBlackboard(_agent.Blackboard);

            AppendDecisions(_agent.Context.Trace);
            return _text.ToString();
        }

        private void Awake()
        {
            if (_agent == null)
                _agent = GetComponentInParent<AICharacter>();
            if (_agent == null)
                _agent = transform.root.GetComponentInChildren<AICharacter>(true);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (_agent != null)
                _agent.IsTracing = true;
#endif
        }

        private void AppendStates()
        {
            BehaviorGraphInstance graph = _agent.Graph;
            if (graph == null || !graph.IsRunning)
            {
                _text.Append(_agent.IsThinking ? "(no state)" : "(brain off)").Append('\n');
                return;
            }

            for (int i = 0; i < graph.ActivePath.Count; i++)
            {
                if (i > 0)
                    _text.Append(" / ");
                _text.Append(graph.ActivePath[i].DisplayName);
            }

            if (!_agent.IsThinking)
                _text.Append("  (brain off)");
            _text.Append('\n');

            AgentState leaf = graph.Current;
            for (int i = 0; i < leaf.Tasks.Count; i++)
            {
                AgentTask task = leaf.Tasks[i];
                _text.Append("  ").Append(task.DisplayName).Append(' ')
                    .Append(task.IsRunningFor(_agent.Context) ? "running" : task.StatusOf(_agent.Context).ToString())
                    .Append('\n');
            }
        }

        private void AppendBlackboard(Blackboard blackboard)
        {
            for (int i = 0; i < blackboard.Count; i++)
            {
                if (!blackboard.HasValueAt(i))
                    continue;                       // removed

                _text.Append(blackboard.NameOf(i));
                if (blackboard.IsTagAt(i))
                    _text.Append(" (tag)\n");
                else
                    _text.Append(" = ").Append(blackboard.BoxedValueOf(i)).Append('\n');
            }
        }

        private void AppendDecisions(DecisionTrace trace)
        {
            int from = Mathf.Max(0, trace.Count - _decisions);
            for (int i = from; i < trace.Count; i++)
                _text.Append(trace[i]).Append('\n');
        }

#if UNITY_EDITOR
        // Drawing in the Scene view: not run by tests (no Scene view in batch mode), excluded from coverage on purpose
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private void OnDrawGizmos()
        {
            if (!_onlySelected)
                Draw();
        }

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private void OnDrawGizmosSelected()
        {
            if (_onlySelected)
                Draw();
        }

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private void DrawPerception()
        {
            AgentPerception perception = _agent.Context?.Perception;
            if (perception == null)
                return;

            PerceptionProfile profile = perception.Profile;
            AgentBodyInfo body = _agent.Context.Actuator != null ? _agent.Context.Actuator.Body : AgentBodyInfo.Default;
            Vector3 up = body.Up.sqrMagnitude > 1e-6f ? body.Up.normalized : Vector3.up;
            Vector3 eye = body.Position + up * profile.EyeHeight;
            Vector3 right = new Vector3(up.y, -up.x, 0f).normalized;
            // A body in 3D: the cones lie flat around where it looks; a side-view body: in the plane of the screen
            bool looksIn3D = Vector3.ProjectOnPlane(body.Forward, up).sqrMagnitude > 1e-6f;
            Vector3 facing = AgentPerception.LookOf(body, up);
            if (facing == Vector3.zero)
                facing = right;
            Vector3 normal = looksIn3D ? up
                : Vector3.Cross(right, up).sqrMagnitude > 1e-6f ? Vector3.Cross(right, up).normalized : Vector3.forward;

            Handles.color = new Color(1f, 0.85f, 0.2f, 0.6f);
            Handles.DrawWireArc(eye, normal, Quaternion.AngleAxis(-profile.FocusHalfAngle, normal) * facing,
                profile.FocusHalfAngle * 2f, profile.FocusRange);
            Handles.color = new Color(1f, 0.6f, 0.2f, 0.4f);
            Handles.DrawWireArc(eye, normal, Quaternion.AngleAxis(-profile.PeripheryHalfAngle, normal) * facing,
                profile.PeripheryHalfAngle * 2f, profile.PeripheryRange);
            Handles.color = new Color(1f, 1f, 1f, 0.25f);
            Handles.DrawWireDisc(eye, normal, profile.BackRange);

            foreach (PerceivedTarget track in perception.Tracks)
            {
                Vector3 at = track.Visible && track.Transform != null ? track.Transform.position : track.LastKnownPosition;
                Gizmos.color = track.Visible ? Color.Lerp(Color.yellow, Color.red, track.Awareness) : Color.gray;
                Gizmos.DrawLine(eye, at);
                Handles.Label(at + up * 0.3f, track.Awareness.ToString("0.00"));
                if (!track.Visible)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireCube(track.LastKnownPosition, Vector3.one * 0.3f);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private void Draw()
        {
            if (!Application.isPlaying || _agent == null)
                return;

            Vector3 position = _agent.Context != null ? _agent.Context.Position : _agent.transform.position;
            Handles.Label(position + Vector3.up * _height, Describe(), EditorStyles.helpBox);
            if (_perception)
                DrawPerception();

            INavigator navigation = _agent.Context?.Navigation;
            if (!_navigation || navigation == null || !navigation.Status.IsMoving())
                return;

            Gizmos.color = navigation.Status switch
            {
                NavStatus.Waiting => Color.yellow,
                NavStatus.Blocked => new Color(1f, 0.5f, 0f),
                _ => Color.green
            };
            Gizmos.DrawLine(position, navigation.SteeringTarget);
            Gizmos.DrawWireSphere(navigation.SteeringTarget, 0.15f);
        }
#endif
    }
}
