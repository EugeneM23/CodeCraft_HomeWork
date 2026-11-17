using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Everything a node is allowed to touch: the blackboards of the three scopes, the body (through the actuator),
    /// navigation, abilities, the current target, the agent's own random and its local time. A node never reaches for
    /// a component or for UnityEngine.Time — it asks the context.
    /// </summary>
    public sealed partial class AgentContext
    {
        private readonly List<string> _signals = new();
        private readonly Dictionary<string, float> _signalTimes = new();

        private AgentRandom _random;

        public AgentContext(AICharacter agent, Blackboard blackboard, int seed = 0)
        {
            Agent = agent;
            Blackboard = blackboard ?? new Blackboard();
            Reseed(seed);
        }

        public AICharacter Agent { get; }

        /// <summary>Own facts of this agent.</summary>
        public Blackboard Blackboard { get; }

        /// <summary>
        /// Facts shared by the squad; null — the agent is alone. The squad scope of a lone agent is NOT its own
        /// blackboard: it is an empty detached one (a graph written for a squad must not quietly work solo on the
        /// agent's facts), and the first use warns once. Squads come back with the coordinator (stage 8).
        /// </summary>
        public Blackboard SquadBlackboard { get; set; }

        private Blackboard _detachedSquad;

        /// <summary>Facts of the world (alert level, combat phase); null — no world (reads fall back to the agent scope).</summary>
        public Blackboard GlobalBlackboard { get; set; }

        public IAgentActuator Actuator { get; set; }

        /// <summary>Line of sight (an adapter's physics); null — every line is clear.</summary>
        public ISightProbe Sight { get; set; }

        /// <summary>Ground under a point (edges, pits); null — unknown.</summary>
        public IGroundProbe Ground { get; set; }

        /// <summary>How far the body can go straight from a point (walls, edges); null — every way is free.</summary>
        public IWalkProbe Walk { get; set; }

        /// <summary>What the character holds in its hands (a rock, a box of ammo); null — it carries nothing ever.</summary>
        public IHands Hands { get; set; }

        /// <summary>"Is an object of this kind near me" for the graph's sensors: by tag unless the game gives its own.</summary>
        public IObjectFinder Objects { get; set; } = TagObjectFinder.Shared;

        /// <summary>"A place somewhere on this object" for the Patrol Area node: the object's drawn box unless the game gives its own.</summary>
        public IAreaSampler Areas { get; set; } = BoundsAreaSampler.Shared;

        /// <summary>What the character attacks with (the Attack node's moves); null — the node does not strike.</summary>
        public WeaponAsset Weapon { get; set; }

        /// <summary>The weapons the character carries and where their models are (the Switch Weapon node); null — none.</summary>
        public IWeaponRack Rack { get; set; }

        /// <summary>
        /// The body's way to let a projectile fly — an arrow, a thrown rock, a grenade (straight or a lob); null — the
        /// projectile lands at once (Projectiles.LandAtOnce).
        /// </summary>
        public IProjectileLauncher Launcher { get; set; }

        /// <summary>How many sight queries reached the probe (profiling and the "asked once per think" test).</summary>
        public int SightQueries { get; internal set; }

        private int _sightStamp;
        private bool _sightAnswer;

        /// <summary>
        /// A clear line from the agent's eyes to the target. Inside a think tick it is asked of the probe once and the
        /// answer is shared by every condition of that tick (handbook: world queries are cached per think); outside a
        /// think each call asks again.
        /// </summary>
        public bool CanSeeTarget()
        {
            if (!HasTarget)
                return false;

            bool cached = ThinkStamp != 0 && _aim == null;      // an aimed ask is about another object: not shared
            if (cached && ThinkStamp == _sightStamp)
                return _sightAnswer;

            bool answer = true;
            if (Sight != null)
            {
                SightQueries++;
                answer = Sight.HasLineOfSight(EyePosition, TargetPosition);
            }

            if (cached)
            {
                _sightStamp = ThinkStamp;
                _sightAnswer = answer;
            }

            return answer;
        }

        /// <summary>Where the agent looks from: the top of the body.</summary>
        public Vector3 EyePosition
        {
            get
            {
                if (Actuator == null)
                    return Position;

                AgentBodyInfo body = Actuator.Body;
                return body.Position + body.Up * (body.Height * 0.9f);
            }
        }

        public INavigator Navigation { get; set; }

        public AgentAbilities Abilities { get; set; }

        /// <summary>The agent's own transform (gizmos, distances when there is no actuator).</summary>
        public Transform Self { get; set; }

        /// <summary>
        /// The object the Brain thinks about (wired into its Target pin), while a decision's chain runs for it. A block
        /// of that chain with no Target of its own works with it — the canvas marks such a block "target: Brain's". Null
        /// — no decision is in charge, or the Brain sees nothing: a block anywhere else gets no hidden target.
        /// </summary>
        public Transform BrainTarget { get; set; }

        /// <summary>
        /// The squad the agent marches with: its AI Squad lists it. Null — the agent is no squad's member. The member's
        /// Follow Squad and Keep Formation ask it for their slot; its Brain hands it what it sees.
        /// </summary>
        public Squad Squad { get; internal set; }

        /// <summary>
        /// The squad this context commands — set only on the context of an AI Squad's command graph, whose blocks lead
        /// the formation (March To Target, Form Ring…). Null on every character.
        /// </summary>
        public Squad Leads { get; internal set; }

        /// <summary>
        /// Somebody looks at this agent's decisions (the graph window in Play): the Brain writes out why each decision
        /// does not hold, with its numbers. Off — the decisions only answer, and a think allocates nothing.
        /// </summary>
        public bool ExplainsDecisions { get; set; }

        /// <summary>What the agent knows about its target (subject, awareness, memory); perception fills it.</summary>
        public TargetTrack Track { get; } = new();

        /// <summary>
        /// The live target's transform (a convenience read of Track). Setting it is the debug and test shortcut for
        /// Track.Set: a chosen subject with full awareness.
        /// </summary>
        public Transform Target
        {
            get => _aim != null ? _aim : Track.Transform;
            set => Track.Set(value);
        }

        private Transform _aim;

        /// <summary>
        /// Points the agent at an object for the length of one ask (dispose the answer to end it). A node of the
        /// canvas works with the object of a variable — what a sensor sees — not with the target perception tracks:
        /// while the aim is set, Target, HasTarget, TargetPosition, DistanceToTarget, CanSeeTarget and the
        /// engagement answer about that object, so abilities and their conditions work for both kinds of agent.
        /// Null — no aim: the agent's own target.
        /// </summary>
        public AimScope AimAt(Transform target)
        {
            var scope = new AimScope(this, _aim);
            _aim = target;
            return scope;
        }

        /// <summary>The aim of one ask; disposing it gives the agent its previous aim back.</summary>
        public readonly struct AimScope : IDisposable
        {
            private readonly AgentContext _context;
            private readonly Transform _previous;

            internal AimScope(AgentContext context, Transform previous)
            {
                _context = context;
                _previous = previous;
            }

            public void Dispose()
            {
                if (_context != null)
                    _context._aim = _previous;
            }
        }

        /// <summary>How aware the agent is of what it is asked about: fully — of an object it was pointed at.</summary>
        public float TargetAwareness => _aim != null ? 1f : Track.Awareness;

        /// <summary>Where the target was seen last — the graph searches by memory, not by the live position.</summary>
        public Vector3 LastKnownTargetPosition
        {
            get => Track.LastKnownPosition;
            set => Track.LastKnownPosition = value;
        }

        /// <summary>Seconds since the agent started (accumulated from dt, never UnityEngine.Time).</summary>
        public float LocalTime { get; private set; }

        private int _thinkCount;

        /// <summary>
        /// Non-zero while a think tick runs (a number unique to that tick): conditions answer once per think. Zero
        /// outside — tasks and abilities evaluate every time.
        /// </summary>
        public int ThinkStamp { get; private set; }

        /// <summary>A think tick starts (the graph calls it around Start and Think).</summary>
        public void BeginThink()
        {
            _thinkCount = _thinkCount == int.MaxValue ? 1 : _thinkCount + 1;
            ThinkStamp = _thinkCount;
            NextAnswerStamp();
        }

        public void EndThink()
        {
            ThinkStamp = 0;
            AnswerStamp = 0;
        }

        private int _answerCount;

        /// <summary>
        /// Non-zero while a think tick runs: the answers of conditions and value nodes are kept under it (asked again —
        /// the same answer). A block of the flow that has done its work within the think starts a new one (ForgetAnswers),
        /// so the questions after it see what it changed — a Branch in a loop of instant blocks sees the loop's count.
        /// </summary>
        public int AnswerStamp { get; private set; }

        /// <summary>A block has done its work within the think: the questions asked from now on are answered anew.</summary>
        public void ForgetAnswers()
        {
            if (ThinkStamp != 0)
                NextAnswerStamp();
        }

        private void NextAnswerStamp()
        {
            _answerCount = _answerCount == int.MaxValue ? 1 : _answerCount + 1;
            AnswerStamp = _answerCount;
        }

        /// <summary>On — decisions are recorded into Trace; off — recording costs one branch.</summary>
        public bool TraceEnabled { get; set; }

        /// <summary>The last decisions of the agent as data (DecisionRecord.ToString formats one for people).</summary>
        public DecisionTrace Trace { get; } = new();

        public bool HasTarget => _aim != null || Track.IsValid;

        public Vector3 Position =>
            Actuator != null ? Actuator.Body.Position : Self != null ? Self.position : Vector3.zero;

        public Vector3 TargetPosition => _aim != null ? _aim.position : Track.Position;

        public Vector3 ToTarget => TargetPosition - Position;

        public float DistanceToTarget => Vector3.Distance(Position, TargetPosition);

        public Blackboard BlackboardOf(BlackboardScope scope) =>
            scope switch
            {
                BlackboardScope.Squad => SquadBlackboard ?? DetachedSquad(),
                BlackboardScope.Global => GlobalBlackboard ?? Blackboard,
                _ => Blackboard
            };

        private Blackboard DetachedSquad()
        {
            if (_detachedSquad != null)
                return _detachedSquad;

            _detachedSquad = new Blackboard();
            Debug.LogWarning($"AI Kit: agent '{(Self != null ? Self.name : "?")}' uses the Squad scope but has no " +
                "squad — its squad variables live in a detached blackboard, not in the agent's own. Squads are not " +
                "supported until the coordinator returns (stage 8).");
            return _detachedSquad;
        }

        public T GetVariable<T>(BlackboardScope scope, string name, T fallback = default) =>
            string.IsNullOrEmpty(name) ? fallback : BlackboardOf(scope).Get(name, fallback);

        public void SetVariable<T>(BlackboardScope scope, string name, T value)
        {
            if (!string.IsNullOrEmpty(name))
                BlackboardOf(scope).Set(name, value);
        }

        /// <summary>A signal reached the agent; it lives until the end of the next think tick.</summary>
        public void SendSignal(string id)
        {
            if (!string.IsNullOrEmpty(id) && !_signals.Contains(id))
                _signals.Add(id);
            if (!string.IsNullOrEmpty(id))
                _signalTimes[id] = LocalTime;
        }

        private readonly Dictionary<string, Transform> _signalSubjects = new();

        /// <summary>A signal of the bus reached the agent: as SendSignal, and what it was about is kept with it.</summary>
        public void HearSignal(in Signal signal)
        {
            SendSignal(signal.Id);
            if (!string.IsNullOrEmpty(signal.Id))
                _signalSubjects[signal.Id] = signal.Subject;
        }

        /// <summary>What the last heard signal with this id was about (the one an ally called about); null — nothing.</summary>
        public Transform SignalSubject(string id) =>
            !string.IsNullOrEmpty(id) && _signalSubjects.TryGetValue(id, out Transform subject) ? subject : null;

        public bool HasSignal(string id) => !string.IsNullOrEmpty(id) && _signals.Contains(id);

        /// <summary>The signal reached the agent within the last seconds (a memory of it, unlike HasSignal).</summary>
        public bool HasSignalSince(string id, float seconds) =>
            !string.IsNullOrEmpty(id) && _signalTimes.TryGetValue(id, out float at) && LocalTime - at <= seconds;

        /// <summary>
        /// The agent's senses, opened by a Senses sensor of its graph (their state is the sensor's); null — it has none
        /// (its target comes from the graph's other sensors, or the game: SetTarget).
        /// </summary>
        public AgentPerception Perception { get; set; }

        /// <summary>The bus the agent shouts on and listens to; null — it is alone.</summary>
        public ISignalBus Signals { get; set; }

        /// <summary>Who writes which axis of the body this step (invariant 4).</summary>
        public MovementArbiter Arbiter { get; } = new();

        /// <summary>What this agent remembers about the nodes it runs (a graph itself is shared and holds none).</summary>
        public NodeMemory Memory { get; } = new();

        /// <summary>The agents of the agent's world; null — no world (then nothing limits attacks).</summary>
        public AgentRoster Roster => Agent != null ? Agent.World?.Roster : null;

        /// <summary>The engagement around the current target (null — no target or no world).</summary>
        public Engagement Engagement => Roster?.EngagementOf(Target);

        /// <summary>
        /// Asks the current target's tokens of a pool (the request is refreshed every call: keep asking while you want to
        /// attack). Without a world nothing limits attacks — Granted; without a target there is nothing to ask — Refused.
        /// </summary>
        public TokenResult RequestToken(string pool, int cost = 1, float priority = 0f)
        {
            if (Roster == null)
                return TokenResult.Granted;

            Engagement engagement = Engagement;
            return engagement != null
                ? engagement.Tokens.Request(Agent, pool, cost, priority, DistanceToTarget)
                : TokenResult.Refused;
        }

        public bool HoldsToken(string pool) => Roster == null || (Engagement?.Tokens.Holds(Agent, pool) ?? false);

        /// <summary>A token of the pool could come now or soon (held, or room in a pool that is not resting).</summary>
        public bool CouldWinToken(string pool) => Roster == null || (Engagement?.Tokens.CouldWin(Agent, pool) ?? false);

        /// <summary>Every token and request of the agent on every target goes (it died, it leaves).</summary>
        public void ReleaseAllTokens()
        {
            AgentRoster roster = Roster;
            if (roster == null)
                return;

            foreach (Engagement engagement in roster.Engagements)
                engagement.Tokens.Forget(Agent);
        }

        /// <summary>Lets go of the pool's token (or request) on every target — the target may have changed meanwhile.</summary>
        /// <summary>The agent gives back every place to attack from that it holds, on every target, of every group.</summary>
        public void ReleasePlaces()
        {
            AgentRoster roster = Roster;
            if (roster == null)
                return;

            foreach (Engagement engagement in roster.Engagements)
                engagement.Tokens.ReleasePlaces(Agent);
        }

        public void ReleaseToken(string pool)
        {
            AgentRoster roster = Roster;
            if (roster == null)
                return;

            foreach (Engagement engagement in roster.Engagements)
                engagement.Tokens.Release(Agent, pool);
        }

        public void ClearSignals() => _signals.Clear();

        /// <summary>Records a decision when tracing is on; callers build the record only under TraceEnabled.</summary>
        public void Record(in DecisionRecord record)
        {
            if (TraceEnabled)
                Trace.Add(record);
        }

        /// <summary>Moves the agent's own clock; the agent calls it once per step (tests and custom runners too).</summary>
        public void Advance(float dt)
        {
            WakeUp();                               // the first step: the home is where the character woke up
            LocalTime += dt;
            Track.Advance(dt);
        }

        /// <summary>0 — a new random seed; the same seed gives the same decisions (tests).</summary>
        /// <summary>
        /// The seed the random started from. 0 asked for "any": a seed is then picked and kept here, so even an agent
        /// without a world can be replayed — log it, set it back.
        /// </summary>
        public int Seed { get; private set; }

        /// <summary>The whole state of the random: a save keeps it, a load puts it back and the decisions go on.</summary>
        public ulong RandomState
        {
            get => _random.State;
            set => _random.State = value;
        }

        public void Reseed(int seed)
        {
            Seed = seed != 0 ? seed : AgentRandom.Derive(System.Environment.TickCount, ++_anySeeds);
            _random = new AgentRandom(Seed);
        }

        private static int _anySeeds;

        /// <summary>Uniform in [0, 1).</summary>
        public double NextRandom() => _random.NextDouble();

        public float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        /// <summary>Uniform integer from min to max, both inclusive.</summary>
        public int Range(int min, int max) => max <= min ? min : _random.Next(min, (int)System.Math.Min((long)max + 1, int.MaxValue));

        public bool Chance(float probability) =>
            probability >= 1f || (probability > 0f && _random.NextDouble() < probability);
    }
}
