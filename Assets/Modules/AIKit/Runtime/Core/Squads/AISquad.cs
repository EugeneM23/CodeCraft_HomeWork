using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A squad as a scene object: its command graph and its characters. The object is the squad's banner — where the
    /// squad forms up and which way it faces. Once per step (after the world, before the characters) the members that are
    /// awake are the squad, the command graph thinks (on its interval) and its blocks lead the formation, and every member
    /// gets its slot. The command graph moves only the formation (handbook §21: the coordinator; §20a: slots round a point
    /// of the squad's own); each character runs its own graph and keeps its slot by its own Follow Squad — or does
    /// something more important of its own and comes back. A character listed by two squads marches in the last one.
    /// </summary>
    [DefaultExecutionOrder(-175)]
    [AddComponentMenu("AI Kit/AI Squad")]
    public sealed class AISquad : MonoBehaviour
    {
        [Tooltip("The squad's command graph: a Squad Brain, its decisions and the blocks that lead the formation")]
        [SerializeField] private BehaviorGraphAsset _graph;

        [Tooltip("The characters of the squad")]
        [SerializeField] private List<AICharacter> _members = new();

        [Tooltip("Seconds between the command graph's decisions (its blocks lead the formation every step)")]
        [SerializeField] private float _thinkInterval = 0.1f;

        [Tooltip("Scene objects for the command graph's Transform variables (where the squad gathers, who stands in the middle)")]
        [SerializeField] private List<ObjectBinding> _bindings = new();

        private BehaviorGraphInstance _instance;
        private float _thinkTimer;
        private bool _initialized;

        /// <summary>The command graph asset the squad runs.</summary>
        public BehaviorGraphAsset GraphAsset
        {
            get => _graph;
            set => _graph = value;
        }

        /// <summary>The characters of the squad (change the list freely: the squad follows it every step).</summary>
        public List<AICharacter> Members => _members;

        public float ThinkInterval
        {
            get => _thinkInterval;
            set => _thinkInterval = Mathf.Max(0f, value);
        }

        /// <summary>The scene objects given to the command graph's Transform variables.</summary>
        public IReadOnlyList<ObjectBinding> Bindings => _bindings;

        /// <summary>The object bound to a variable (null — none).</summary>
        public Transform BoundTo(string variable)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].Variable == variable)
                    return _bindings[i].Object;
            }

            return null;
        }

        /// <summary>Binds a variable of the command graph to an object (null unbinds); a running squad sees it at once.</summary>
        public void Bind(string variable, Transform target)
        {
            if (string.IsNullOrEmpty(variable))
                return;

            ObjectBinding binding = _bindings.Find(candidate => candidate.Variable == variable);
            if (binding == null)
            {
                binding = new ObjectBinding { Variable = variable };
                _bindings.Add(binding);
            }

            binding.Object = target;
            Context?.Blackboard.Set(variable, target);
        }

        // The squad's own objects reach the command graph's blackboard
        private void ApplyBindings()
        {
            if (Context == null)
                return;

            for (int i = 0; i < _bindings.Count; i++)
            {
                if (!string.IsNullOrEmpty(_bindings[i].Variable))
                    Context.Blackboard.Set(_bindings[i].Variable, _bindings[i].Object);
            }
        }

        // An object dragged into a binding in Play Mode reaches the running squad
        private void OnValidate() => ApplyBindings();

        /// <summary>The squad: its members, formation and slots.</summary>
        public Squad Squad { get; private set; }

        /// <summary>The command graph's context (its blackboard, its target); Leads is the squad.</summary>
        public AgentContext Context { get; private set; }

        /// <summary>The running command graph (null — none).</summary>
        public BehaviorGraphInstance Graph => _instance;

        public void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            Squad = new Squad(transform);
            Context = new AgentContext(null, new Blackboard()) { Self = transform, Leads = Squad };
            ApplyBindings();
            if (_instance == null && _graph != null)
                SetGraph(_graph.Compile());
        }

        /// <summary>Gives the squad a command graph; the previous one is stopped.</summary>
        public void SetGraph(BehaviorGraph graph)
        {
            Initialize();
            _instance?.Stop(Context);
            Context.Memory.Clear();
            _instance = graph?.CreateInstance();
            _thinkTimer = 0f;
        }

        /// <summary>
        /// One step of the squad: the members that are awake join (those that are not leave — a fallen one's slot goes to
        /// the one behind it; so do those taken off the list or destroyed), the command graph thinks when its time has
        /// come, its blocks lead the formation, the slots are placed. The game calls it from FixedUpdate; tests step it by
        /// hand.
        /// </summary>
        public void Step(float dt)
        {
            Initialize();
            for (int i = 0; i < _members.Count; i++)
            {
                AICharacter member = _members[i];
                if (member == null || member.Context == null)
                    continue;

                if (member.enabled && member.gameObject.activeInHierarchy)
                    Squad.Join(member.Context);
                else
                    Squad.Leave(member.Context);
            }

            Squad.KeepOnly(_members);
            Squad.Begin(dt);
            Context.Advance(dt);
            if (_instance != null)
            {
                _thinkTimer -= dt;
                if (_thinkTimer <= 0f)
                {
                    _thinkTimer = _thinkInterval;
                    _instance.Think(Context);
                }

                _instance.Tick(Context, dt);
            }

            Squad.End();
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        // Switched off: nobody marches in it any more (the ones taken off the list since its last step too)
        private void OnDisable()
        {
            if (!_initialized)
                return;

            _instance?.Stop(Context);
            Squad.LeaveAll();
        }
    }
}
