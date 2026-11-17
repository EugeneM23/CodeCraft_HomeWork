using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// Runs a behavior graph for one agent — the canvas as the program, and nothing else:
    /// - the graph starts in its Brain (a graph without one does nothing — the agent stands);
    /// - the sensors (the root's background tasks) run every step and write their variables;
    /// - Think(): the Brain counts the facts about its Target once; the On Tick chain of the block the agent is in runs;
    ///   the node's own arrows lead on (the Brain takes a decision, a decision's Do leads to its first block, a block's
    ///   output to the next block); then the Brain looks again (Supervise) — a more important decision takes over,
    ///   never in the middle of a blow or an ability; a chain that has come to its end hands the choice back to the
    ///   Brain; a repeating block (Strike) with no arrow from the output it ended through runs again.
    /// A think tick is opened on the context, so every question is answered at most once in it. Tick(): the sensors and
    /// the node the agent is in.
    /// </summary>
    public sealed class BehaviorGraphInstance
    {
        private readonly BehaviorGraph _graph;

        // The root (its sensors run while the graph runs) and the node the agent is in: [root, node]
        private readonly List<AgentState> _active = new();

        /// <summary>An agent's run of a graph. Everything the nodes remember lives in the agent's context (NodeMemory).</summary>
        public BehaviorGraphInstance(BehaviorGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        /// <summary>Previous node → new node.</summary>
        public event Action<AgentState, AgentState> StateChanged;

        public BehaviorGraph Graph => _graph;

        /// <summary>The graph's root: the sensors and every node.</summary>
        public AgentState Root => _graph.Root;

        /// <summary>The node the agent is in (the Brain, a decision, a block of a chain); null — the graph does not run.</summary>
        public AgentState Current => _active.Count > 1 ? _active[_active.Count - 1] : null;

        /// <summary>The root and the node the agent is in.</summary>
        public IReadOnlyList<AgentState> ActivePath => _active;

        public bool IsRunning => _active.Count > 0;

        /// <summary>
        /// Enters a node directly — a loaded save, a debug jump. Only the Brain, a decision of it or a block a decision's
        /// chain reaches: a block is handed to the decision whose chain it is, a decision to the Brain. False — the node
        /// never runs (no chain reaches it) or the graph has no Brain.
        /// </summary>
        public bool EnterState(AgentState state, AgentContext context)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (state.Parent != Root)
                throw new ArgumentException($"'{state.DisplayName}' is not a node of the graph this agent runs");

            if (!_graph.Runs(state))
                return false;

            bool owns = BeginThink(context);
            try
            {
                if (!IsRunning)
                    EnterRoot(context);

                AgentState previous = Current;
                _clock = context != null ? context.LocalTime : _clock;
                Go(state, context, restart: true);
                DropCharge(context);
                AgentState decision = _graph.ChainOwnerOf(state);
                if (decision != null)
                    Charge(_graph.Brain, decision, context);

                if (context != null && context.TraceEnabled)
                    context.Record(new DecisionRecord(context.LocalTime, DecisionKind.Entered, previous, Current));

                return true;
            }
            finally
            {
                EndThink(context, owns);
            }
        }

        /// <summary>EnterState by the node's name (the first match).</summary>
        public bool EnterState(string name, AgentContext context)
        {
            AgentState state = Root?.Children.Find(node => node != null && node.Name == name);
            if (state == null)
                throw new ArgumentException($"No node named '{name}' in the graph this agent runs");

            return EnterState(state, context);
        }

        /// <summary>EnterState by the node's stable id (a loaded save keeps the id, not the name).</summary>
        public bool EnterState(int id, AgentContext context)
        {
            AgentState state = Root?.Children.Find(node => node != null && node.Id == id);
            if (state == null)
                throw new ArgumentException($"No node with id {id} in the graph this agent runs");

            return EnterState(state, context);
        }

        public void Start(AgentContext context)
        {
            if (IsRunning)
                return;

            bool owns = BeginThink(context);
            try
            {
                StartCore(context);
            }
            finally
            {
                EndThink(context, owns);
            }
        }

        public void Think(AgentContext context)
        {
            bool owns = BeginThink(context);
            try
            {
                ThinkCore(context);
            }
            finally
            {
                EndThink(context, owns);
            }
        }

        /// <summary>Opens a think tick on the context unless one is already open (Think calls Start).</summary>
        private static bool BeginThink(AgentContext context)
        {
            if (context == null || context.ThinkStamp != 0)
                return false;

            context.BeginThink();
            return true;
        }

        private static void EndThink(AgentContext context, bool owns)
        {
            if (owns)
                context.EndThink();
        }

        // The graph starts in its Brain; without one nothing runs at all — not even the sensors
        private void StartCore(AgentContext context)
        {
            if (_graph.Brain == null)
                return;

            EnterRoot(context);
            Go(_graph.Brain, context, restart: false);
            if (context != null && context.TraceEnabled)
                context.Record(new DecisionRecord(context.LocalTime, DecisionKind.Start, to: Current));
        }

        private void EnterRoot(AgentContext context)
        {
            DeclareVariables(context);
            Root.Enter(context);
            _active.Add(Root);
        }

        /// <summary>The variables the graph declares come to the agent's blackboard (kept if already there).</summary>
        private void DeclareVariables(AgentContext context)
        {
            if (context == null)
                return;

            for (int i = 0; i < _graph.Variables.Count; i++)
                _graph.Variables[i].DeclareOn(context.BlackboardOf(_graph.Variables[i].Scope));
        }

        private void ThinkCore(AgentContext context)
        {
            if (!IsRunning)
            {
                StartCore(context);
                return;
            }

            AgentState node = Current;
            if (node == null)
                return;

            _clock = context != null ? context.LocalTime : _clock;
            WriteBrainFacts(context);
            RunTicks(context);

            if (node.IsBrain)
            {
                if (TakeDecision(node, null, context))
                    Follow(context);
                return;
            }

            // Nobody's hands (a node entered directly that no decision owns any more): the Brain chooses
            if (_decision == null)
            {
                if (TakeDecision(_graph.Brain, null, context))
                    Follow(context);
                return;
            }

            if (TryFire(node, context))
            {
                Follow(context);

                // The chain has moved on — the Brain still looks once in this think (handbook §2.4: after the chain's
                // own arrows): a chain of quick blocks must not keep a more important decision waiting
                if (LookAgain(context))
                    Follow(context);
                return;
            }

            if (Supervise(context))
            {
                Follow(context);
                return;
            }

            // A repeating block (Strike) whose output leads nowhere runs again: the next blow
            if (Current.HasForegroundTasks && Current.TasksFinishedFor(context) && Current.HasRepeatingTasks &&
                !Current.AnyTaskFailedFor(context))
                Current.RestartTasks(context);
        }

        /// <summary>How many nodes the flow may pass within one think (a loop of instant blocks stops here).</summary>
        private const int MOST_PASSED_NODES = 16;

        // Within one think the flow passes what takes no time (handbook §2.2: instant nodes pass in one tick): a
        // decision's Do leads on at once, and an instant block (Set, Add, Branch, Debug Log, Random) runs and leads on by
        // its outcome — the agent never stands a think in a node that does nothing
        private void Follow(AgentContext context)
        {
            for (int hop = 0; hop < MOST_PASSED_NODES && Current != null; hop++)
            {
                AgentState node = Current;
                if (node.IsBrain)
                    return;

                if (node.Reasoner != null)
                {
                    if (!TryFire(node, context))
                        return;                     // a decision with no chain: it holds the agent

                    continue;
                }

                if (!node.IsInstant || !node.HasForegroundTasks)
                    return;

                node.Tick(context, 0f);             // its work takes no time: done now
                if (!TryFire(node, context))
                    return;                         // the end of the chain: the Brain sees it on the next think
            }
        }

        // The Brain counts the facts about its Target once per think, before anything asks them. The blocks of a
        // decision's chain work with that target (BrainTarget) — only while a decision is in charge: a block anywhere
        // else gets no hidden value (the project's rule: no implicit variables)
        private void WriteBrainFacts(AgentContext context)
        {
            if (context == null)
                return;

            AgentState brain = _graph.Brain;
            if (brain == null)
            {
                context.BrainTarget = null;
                return;
            }

            TargetFacts facts = CombatBrainTask.FactsOf(brain, context);
            context.BrainTarget = _decision != null ? facts.Target : null;

            // Every decision of the Brain looks at the world once per think, before any is asked (step 90): events and
            // time are kept here, so the questions that follow only read
            for (int i = 0; i < brain.Transitions.Count; i++)
                brain.Transitions[i].Target?.Reasoner?.Observe(context, facts);

            // What the decision in charge keeps up while its chain runs (Circle asks for a place), before any arrow
            _decision?.Reasoner?.InCharge(context, facts);
            RunDecisionTicks(context);
        }

        private bool _decisionTicked;
        private float _decisionTickedAt;

        // The On Tick pins of the decision in charge (step 90): whatever block of its chain is on, the decision's own
        // chain of instant blocks runs once per think, before any arrow — the visible place for what the decision keeps
        // up while it is in charge (asking for an attack place). When the flow stands in the decision itself, RunTicks
        // runs them as the node's own.
        private void RunDecisionTicks(AgentContext context)
        {
            AgentState decision = _decision;
            if (decision == null || decision == Current)
                return;

            bool any = false;
            for (int i = 0; i < decision.Transitions.Count && !any; i++)
                any = decision.Transitions[i].Trigger == TransitionTrigger.Tick;
            if (!any)
                return;

            float now = context.LocalTime;
            float dt = _decisionTicked ? Math.Max(0f, now - _decisionTickedAt) : 0f;
            _decisionTicked = true;
            _decisionTickedAt = now;
            context.Blackboard.Set(DELTA_TIME, dt);
            RunTickPins(decision, context, dt);
        }

        /// <summary>The number of the blackboard the On Tick chain reads: seconds since its last run.</summary>
        public const string DELTA_TIME = "Delta Time";

        private const int MOST_TICK_NODES = 32;

        private AgentState _tickedLeaf;
        private float _tickedAt;

        /// <summary>
        /// The On Tick pins of the node: each chain of instant blocks runs to its end, then the flow is back in the node
        /// — it was never left. Once per think (handbook §3.2), before the node's own arrows, so what a chain writes is
        /// seen by them in the same think. Delta Time is the time since the last run in this node.
        /// </summary>
        private void RunTicks(AgentContext context)
        {
            AgentState leaf = Current;
            if (leaf == null || context == null)
                return;

            bool any = false;
            for (int i = 0; i < leaf.Transitions.Count && !any; i++)
                any = leaf.Transitions[i].Trigger == TransitionTrigger.Tick;
            if (!any)
            {
                _tickedLeaf = null;
                return;
            }

            float now = context.LocalTime;
            float dt = _tickedLeaf == leaf ? Math.Max(0f, now - _tickedAt) : 0f;
            _tickedLeaf = leaf;
            _tickedAt = now;
            context.Blackboard.Set(DELTA_TIME, dt);

            RunTickPins(leaf, context, dt);
        }

        // Every On Tick pin of a node: its chain of instant blocks runs to its end
        private static void RunTickPins(AgentState owner, AgentContext context, float dt)
        {
            for (int i = 0; i < owner.Transitions.Count; i++)
            {
                if (owner.Transitions[i].Trigger != TransitionTrigger.Tick)
                    continue;

                AgentState node = owner.Transitions[i].Target;
                for (int step = 0; node != null && node.IsInstant && step < MOST_TICK_NODES; step++)
                {
                    bool failed = false;
                    for (int t = 0; t < node.Tasks.Count; t++)
                    {
                        AgentTask task = node.Tasks[t];
                        if (task.IsBackground)
                            continue;

                        TaskStatus status = task.Tick(context, dt);
                        if (status == TaskStatus.Running)
                            task.Interrupt(context);
                        failed |= status != TaskStatus.Success;
                    }

                    AgentState next = null;
                    PickRandomTask random = node.Random;
                    int picked = random != null ? random.Pick(context, node) : -1;
                    for (int a = 0; a < node.Transitions.Count && next == null; a++)
                    {
                        if (random != null && node.Transitions[a].Output >= 0 && node.Transitions[a].Output != picked)
                            continue;

                        TransitionTrigger trigger = node.Transitions[a].Trigger;
                        if (trigger == TransitionTrigger.TasksFinished ||
                            trigger == (failed ? TransitionTrigger.TasksFailed : TransitionTrigger.TasksSucceeded))
                            next = node.Transitions[a].Target;
                    }

                    node = next;
                }
            }
        }

        /// <summary>Stops the graph (kept for callers of the per-agent era: a graph is no longer owned).</summary>
        public void Release(AgentContext context) => Stop(context);

        public void Tick(AgentContext context, float dt)
        {
            for (int i = 0; i < _active.Count; i++)
                _active[i].Tick(context, dt);
        }

        public void Stop(AgentContext context)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
                _active[i].Exit(context, ExitReason.Stopped);

            _active.Clear();
            DropCharge(context);
        }

        // The node's own arrows: a decision's Do, a block's output (of a Random — only the picked one's). An arrow into
        // the Brain or into a decision is the end of the chain: the Brain chooses
        private bool TryFire(AgentState owner, AgentContext context)
        {
            int picked = -1;
            PickRandomTask random = owner.Random;
            if (random != null && owner.TasksFinishedFor(context))
                picked = random.Pick(context, owner);

            for (int i = 0; i < owner.Transitions.Count; i++)
            {
                Transition transition = owner.Transitions[i];
                if (random != null && transition.Output >= 0 && transition.Output != picked)
                    continue;

                if (transition.Target == null || !transition.Check(context, owner))
                    continue;

                AgentState target = transition.Target;
                if (target.IsBrain || target.Reasoner != null)
                    return _brain != null && TakeDecision(_brain, _decision, context, current: true, hold: false);

                AgentState previous = Current;
                Go(target, context, restart: true);
                if (context != null && context.TraceEnabled)
                    context.Record(new DecisionRecord(context.LocalTime, DecisionKind.Transition, previous, Current,
                        transition));

                return true;
            }

            return false;
        }

        // The agent goes into the node: the node it leaves is exited, the new one entered. Into the node it is in already:
        // restart — its block runs anew (an arrow into a node starts it), otherwise nothing changes
        private void Go(AgentState target, AgentContext context, bool restart)
        {
            AgentState previous = Current;
            if (previous == target)
            {
                if (restart)
                    target.RestartTasks(context);
                return;
            }

            if (previous != null)
            {
                previous.Exit(context, ExitReason.Interrupted);
                _active.RemoveAt(_active.Count - 1);
            }

            target.Enter(context);
            _active.Add(target);
            StateChanged?.Invoke(previous, Current);
        }

        // ---------- The Brain's decisions (reasoners) ----------

        /// <summary>A decision just taken is kept at least this long (handbook §1.2 MinStateTime, §19.4, B.7: no flicker).</summary>
        public const float MIN_DECISION_TIME = 0.3f;

        /// <summary>
        /// A decision whose chain ended without any of its blocks running through a think did nothing (its first block
        /// could not start: out of reach, nothing to throw at, the goal already met): it is not taken again for this
        /// long, so the Brain takes another one instead of taking it again think after think (handbook §4.8, §17).
        /// </summary>
        public const float EMPTY_CHAIN_REST = 0.5f;

        // The Brain in charge and the decision whose chain runs for it now (null — the agent is in nobody's hands)
        private AgentState _brain;
        private AgentState _decision;

        private float _chosenAt = float.NegativeInfinity;
        private bool _chainRan;
        private AgentState _lastChosen;
        private float _clock;

        // The decisions resting after a chain that did nothing, with the time they may be taken again
        private readonly List<AgentState> _resting = new();
        private readonly List<float> _restUntil = new();

        /// <summary>The Brain whose decision runs now (null — the agent is in nobody's hands).</summary>
        public AgentState Brain => _brain;

        /// <summary>The decision (reasoner state) whose chain runs now; null — none.</summary>
        public AgentState Decision => _decision;

        // The Brain hands the agent to a decision: its chain runs for that Brain from now
        private void Charge(AgentState brain, AgentState decision, AgentContext context)
        {
            bool same = _decision != null && _decision == decision;
            if (_decision != null && !same)
            {
                _decision.Reasoner?.LeftCharge(context);
                context?.NotifyDecisionLeft();
            }

            _brain = brain;
            _decision = decision;
            _chainRan = false;
            if (decision != _lastChosen)
            {
                _chosenAt = _clock;             // a decision taken anew starts its minimum time; taken again — it does not
                _lastChosen = decision;
            }

            // The same decision taken again after its chain never left: its On Tick keeps counting its Delta Time
            if (!same)
                _decisionTicked = false;
            if (context == null)
                return;

            TargetFacts facts = CombatBrainTask.FactsOf(brain, context);
            context.BrainTarget = facts.Target;
            decision?.Reasoner?.Taken(context, facts);     // what the decision hands its chain (step 90)
        }

        // Nobody's hands: no decision holds, or the graph stopped. Whatever is taken next is taken anew — its minimum
        // time starts, even for the decision taken last
        private void DropCharge(AgentContext context)
        {
            _decision?.Reasoner?.LeftCharge(context);
            if (_decision != null)
                context?.NotifyDecisionLeft();
            _brain = null;
            _decision = null;
            _lastChosen = null;
            if (context != null)
                context.BrainTarget = null;
        }

        private void Rest(AgentState decision)
        {
            int at = _resting.IndexOf(decision);
            if (at < 0)
            {
                _resting.Add(decision);
                _restUntil.Add(_clock + EMPTY_CHAIN_REST);
            }
            else
            {
                _restUntil[at] = _clock + EMPTY_CHAIN_REST;
            }
        }

        private bool IsResting(AgentState decision)
        {
            int at = _resting.IndexOf(decision);
            return at >= 0 && _clock < _restUntil[at];
        }

        /// <summary>
        /// The Brain chooses among the decisions wired to it: those whose conditions hold and that may be taken from
        /// the one the agent acts on (from; null — from none). The most important wins (Priority 1 first); between
        /// equals the current one keeps, else the one made earlier (handbook §1.2, §29). current — from is still a
        /// candidate; hold — it is still running, so a decision just taken is kept for MIN_DECISION_TIME while it holds
        /// (a chain that has ended holds nothing: the most important one is taken at once). A decision resting after a
        /// chain that did nothing is no candidate. Every decision's verdict is kept on the Brain for the canvas; the
        /// reasons are written out only while somebody looks (AgentContext.ExplainsDecisions) — a think allocates
        /// nothing otherwise. Null — no decision holds.
        /// </summary>
        private Transition ChooseReasoner(AgentState brain, AgentState from, bool current, AgentContext context,
            bool hold = true)
        {
            bool explain = context != null && context.ExplainsDecisions;
            List<ReasonerVerdict> verdicts = CombatBrainTask.VerdictListOf(brain, context);
            verdicts?.Clear();
            TargetFacts facts = CombatBrainTask.FactsOf(brain, context);
            Transition best = null;
            ReasonerTask bestTask = null;
            Transition kept = null;

            for (int i = 0; i < brain.Transitions.Count; i++)
            {
                Transition wire = brain.Transitions[i];
                ReasonerTask task = wire.Target?.Reasoner;
                if (task == null)
                    continue;

                bool isCurrent = wire.Target == from;
                bool holds;
                string whyNot = null;
                if (isCurrent && !current)
                {
                    holds = false;
                    whyNot = "done";
                }
                else if (!isCurrent && task.IsBlockedFrom(from))
                {
                    holds = false;
                    if (explain)
                        whyNot = $"not from {from.DisplayName}";
                }
                else if (IsResting(wire.Target))
                {
                    holds = false;
                    whyNot = "did nothing a moment ago — rests";
                }
                else
                {
                    // No hold: the current decision's chain has ended (step 90 — a reaction lets go then)
                    holds = task.HoldsWithCondition(new ReasonerQuery(context, facts, isCurrent, explain, !hold),
                        out whyNot);
                }

                verdicts?.Add(new ReasonerVerdict(wire.Target, holds, false, holds ? null : whyNot));
                if (!holds)
                    continue;

                if (isCurrent)
                    kept = wire;

                bool better = bestTask == null || task.Priority < bestTask.Priority ||
                    task.Priority == bestTask.Priority &&
                    (isCurrent || best.Target != from && wire.Target.Id < best.Target.Id);
                if (better)
                {
                    best = wire;
                    bestTask = task;
                }
            }

            if (hold && best != null && kept != null && best != kept && !bestTask.OutOfTurn &&
                _clock - _chosenAt < MIN_DECISION_TIME)
            {
                // The more important one waits the moment out: the current decision was taken just now
                if (verdicts != null && explain)
                {
                    for (int i = 0; i < verdicts.Count; i++)
                    {
                        if (verdicts[i].State == best.Target)
                        {
                            verdicts[i] = new ReasonerVerdict(best.Target, true, false,
                                $"holds — waits: {kept.Target.DisplayName} was taken a moment ago");
                        }
                    }
                }

                best = kept;
            }

            if (verdicts != null && best != null)
            {
                for (int i = 0; i < verdicts.Count; i++)
                {
                    if (verdicts[i].State == best.Target)
                        verdicts[i] = new ReasonerVerdict(best.Target, true, true, null);
                }
            }

            return best;
        }

        // The Brain hands the agent to the decision it chose (from — the one the agent acted on, done or left). No
        // decision holds: the agent goes back to the Brain and stands there, the Brain shows "no decision".
        private bool TakeDecision(AgentState brain, AgentState from, AgentContext context, bool current = false,
            bool hold = true)
        {
            if (brain == null)
                return false;

            Transition chosen = ChooseReasoner(brain, from, current, context, hold);
            AgentState previous = Current;
            if (chosen == null)
            {
                DropCharge(context);
                if (Current == brain)
                    return false;

                Go(brain, context, restart: false);
                Trace(context, previous, null);
                return true;
            }

            Go(chosen.Target, context, restart: true);
            Charge(brain, chosen.Target, context);
            Trace(context, previous, chosen);
            return true;
        }

        /// <summary>
        /// While a decision's chain runs, the Brain looks again once per think (after the chain's own arrows — the fixed
        /// order of handbook §2.4): another decision wins — the agent switches to it (a decision just taken is kept for
        /// MIN_DECISION_TIME); none holds — back to the Brain, to stand. The chain has come to its end — a node is done
        /// and no arrow leads on — the Brain chooses again, the same decision included. Never in the middle of a blow
        /// or an ability.
        /// </summary>
        private bool Supervise(AgentContext context)
        {
            if (_brain == null || _decision == null || Current == null || Current == _brain)
                return false;

            // A blow or an ability on its way is not cut short — and it is the chain's work: a chain whose only block
            // was a blow did something, its decision must not rest when it ends
            if (Current.IsCommittedFor(context))
            {
                if (Current != _decision)
                    _chainRan = true;
                return false;
            }

            Transition winner = ChooseReasoner(_brain, _decision, true, context);
            if (winner == null || winner.Target != _decision)
            {
                Switch(winner, context);
                return true;
            }

            // The chain goes on while its node runs; a node whose block repeats goes on by itself while it succeeds
            if (Current == _decision)
                return false;                       // its Do has not led on yet

            bool done = Current.HasForegroundTasks && Current.TasksFinishedFor(context);
            bool repeats = Current.HasRepeatingTasks && !Current.AnyTaskFailedFor(context);
            if (!done || repeats)
            {
                _chainRan = true;                   // a block of the chain is at work
                return false;
            }

            // The chain has come to its end. None of its blocks ever ran through a think — it did nothing: the decision
            // rests a moment, so the Brain takes another instead of taking it again think after think (the agent would
            // stand: steps 70, 74, 76)
            if (!_chainRan)
                Rest(_decision);

            return TakeDecision(_brain, _decision, context, current: true, hold: false);
        }

        /// <summary>
        /// The Brain looks again after the chain's arrows moved it on in this think: another decision wins — the agent
        /// switches to it (not in the middle of a blow or an ability, not before a decision just taken has had its
        /// moment); none holds — back to the Brain. What the chain has done so far is not judged here (Supervise does that
        /// when the chain stands). True — the agent switched.
        /// </summary>
        private bool LookAgain(AgentContext context)
        {
            if (_brain == null || _decision == null || Current == null || Current == _brain ||
                Current.IsCommittedFor(context))
                return false;

            Transition winner = ChooseReasoner(_brain, _decision, true, context);
            if (winner != null && winner.Target == _decision)
                return false;

            Switch(winner, context);
            return true;
        }

        // The agent leaves the decision it acts on: to the winner, or — none holds — back to the Brain, to stand
        private void Switch(Transition winner, AgentContext context)
        {
            AgentState previous = Current;
            AgentState brain = _brain;
            if (winner != null)
            {
                Go(winner.Target, context, restart: true);
                Charge(brain, winner.Target, context);
            }
            else
            {
                DropCharge(context);                // back in the Brain: it takes a decision when one holds
                Go(brain, context, restart: false);
            }

            Trace(context, previous, winner);
        }

        private void Trace(AgentContext context, AgentState previous, Transition rule)
        {
            if (context != null && context.TraceEnabled)
                context.Record(new DecisionRecord(context.LocalTime, DecisionKind.Transition, previous, Current, rule));
        }
    }
}
