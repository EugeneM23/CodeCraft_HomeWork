using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The abilities of one agent: their definitions, their runners, their cooldowns and the choice between them.
    /// Weights, cooldowns, conditions, an anti-repeat penalty and a seeded random as data every agent type can reuse.
    /// A cooldown starts when the ability ENDS, so a long wind-up does not eat its own pause.
    /// </summary>
    public sealed class AgentAbilities
    {
        private sealed class Entry
        {
            public AbilityDefinition Definition;
            public IAbilityRunner Runner;
            public float CooldownLeft;
            public float DelayLeft;
            public bool WasRunning;
        }

        private readonly List<Entry> _entries = new();
        private readonly List<Entry> _candidates = new();
        private readonly List<float> _scores = new();

        private string _lastUsedId;

        public int Count => _entries.Count;

        /// <summary>Id of the ability that is playing now; null — none.</summary>
        public string Running
        {
            get
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (_entries[i].Runner != null && _entries[i].Runner.IsRunning)
                        return _entries[i].Definition.Id;
                }

                return null;
            }
        }

        public bool IsAnyRunning => Running != null;

        /// <summary>The phase of the ability that plays now (None — nothing plays).</summary>
        public AbilityPhase RunningPhase
        {
            get
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    IAbilityRunner runner = _entries[i].Runner;
                    if (runner != null && runner.IsRunning)
                        return runner.Phase;
                }

                return AbilityPhase.None;
            }
        }

        public AbilityPhase PhaseOf(string id) => Find(id)?.Runner?.Phase ?? AbilityPhase.None;

        public string LastUsed => _lastUsedId;

        public void Add(AbilityDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.Id))
                return;

            Entry entry = Find(definition.Id);
            if (entry != null)
            {
                entry.Definition = definition;
                entry.DelayLeft = definition.StartDelay;
                return;
            }

            _entries.Add(new Entry { Definition = definition, DelayLeft = definition.StartDelay });
        }

        /// <summary>Attaches a runner to the definition with the same id (a definition may exist without a runner).</summary>
        public void Register(IAbilityRunner runner)
        {
            if (runner == null || string.IsNullOrEmpty(runner.Id))
                return;

            Entry entry = Find(runner.Id);
            if (entry == null)
            {
                entry = new Entry { Definition = new AbilityDefinition { Id = runner.Id } };
                _entries.Add(entry);
            }

            entry.Runner = runner;
        }

        public bool Has(string id) => Find(id) != null;

        /// <summary>The runner that plays the ability of this id; null — none (yet).</summary>
        public IAbilityRunner RunnerOf(string id) => Find(id)?.Runner;

        /// <summary>The ability of this id goes from the agent (its graph no longer names it); a playing one is cancelled.</summary>
        public void Remove(string id)
        {
            Entry entry = Find(id);
            if (entry == null)
                return;

            if (entry.Runner != null && entry.Runner.IsRunning)
                entry.Runner.Cancel();
            _entries.Remove(entry);
        }

        public AbilityDefinition DefinitionOf(string id) => Find(id)?.Definition;

        public float CooldownLeft(string id) => Find(id)?.CooldownLeft ?? 0f;

        public bool IsRunning(string id)
        {
            Entry entry = Find(id);
            return entry?.Runner != null && entry.Runner.IsRunning;
        }

        /// <summary>Everything the definition asks for is true right now: pause, delay, range, ground, target, runner.</summary>
        public bool IsReady(string id, AgentContext context) => IsReady(Find(id), context);

        /// <summary>
        /// Ready, and — withPlace — an ability that takes turns has a place to be had in its pool right now (the
        /// handbook's filter: "are there tokens enough"). Pure: it only reads.
        /// </summary>
        public bool IsReady(string id, AgentContext context, bool withPlace)
        {
            Entry entry = Find(id);
            return IsReady(entry, context) && (!withPlace || HasPlace(entry.Definition, context));
        }

        // The ability's pool has room on the agent's target, or the agent holds a place there; no pool — always. A question
        // makes no state (invariant 6): the fight round the target is looked up, never made — with none, nobody holds a
        // place of the pool yet, so one is to be had: by the ability's own At Once (its ask sets it on the pool —
        // AbilityPlaces), else unless the target takes nobody of it
        private static bool HasPlace(AbilityDefinition definition, AgentContext context)
        {
            if (context == null || string.IsNullOrEmpty(definition.TokenPool))
                return true;

            AgentRoster roster = context.Roster;
            Transform target = context.Target;
            if (roster == null || target == null)
                return true;

            Engagement engagement = roster.FindEngagement(target);
            if (engagement != null)
                return engagement.Tokens.CouldWin(context.Agent, definition.TokenPool);

            return definition.TokenCapacity > 0 || Engagement.CapacityOf(target, roster, definition.TokenPool) > 0;
        }

        // The agent holds a place to attack from at its target (a fight round it that gives the agent one); no world or no
        // target — nothing limits it. Looked up, never made (a question)
        private static bool HoldsPlace(AgentContext context)
        {
            AgentRoster roster = context.Roster;
            Transform target = context.Target;
            if (roster == null || target == null)
                return true;

            Engagement engagement = roster.FindEngagement(target);
            return engagement != null && engagement.Tokens.HoldsPlace(context.Agent);
        }

        public bool TryUse(string id, AgentContext context) => TryUse(id, context, false);

        /// <summary>
        /// The same; ignoreRest — the ability's own pause (cooldown, start delay) is not asked: the graph keeps the pause
        /// itself, in a number of the blackboard it can see (a canvas graph's Use Ability). Everything else still is.
        /// </summary>
        public bool TryUse(string id, AgentContext context, bool ignoreRest)
        {
            Entry entry = Find(id);
            bool usable = ignoreRest
                ? entry != null && !(entry.Runner != null && entry.Runner.IsRunning) &&
                  BlockOf(entry, context, rested: true) == AbilityBlock.None
                : IsReady(entry, context);
            if (!usable)
                return false;

            _lastUsedId = entry.Definition.Id;
            if (entry.Runner == null)
            {
                entry.CooldownLeft = entry.Definition.Cooldown;     // no runner: fire and forget (prototypes, tests)
                return true;
            }

            entry.Runner.Begin(context);
            entry.WasRunning = entry.Runner.IsRunning;
            if (!entry.WasRunning)
                entry.CooldownLeft = entry.Definition.Cooldown;     // an instant ability is over at once

            return true;
        }

        /// <summary>
        /// Score of the ability as it stands now (also what the decision trace prints). A question: a Random axis is read
        /// without drawing (the agent's random is put back).
        /// </summary>
        public float Score(string id, AgentContext context)
        {
            ulong random = context?.RandomState ?? 0UL;
            try
            {
                return Score(Find(id), context);
            }
            finally
            {
                if (context != null)
                    context.RandomState = random;
            }
        }

        /// <summary>
        /// How one ability is picked among the scored ones. It belongs to the set of abilities (the character's ability list),
        /// not to the graph node that asks, so every node of the agent chooses with the same character. Default — a
        /// roulette among the best two: always taking the maximum makes a fight predictable.
        /// </summary>
        public ISelectionStrategy Strategy { get; set; } = new TopRandomSelection(2);

        /// <summary>
        /// Picks an ability: the ready options are scored (a required axis without data vetoes one — recorded as such,
        /// not as a zero), and the strategy takes one of those that scored above zero.
        /// Null — nothing is available right now.
        /// </summary>
        public string Select(AgentContext context) => Select(context, false);

        /// <summary>The same choice; withPlace — among the abilities that have a place in their pool right now.</summary>
        public string Select(AgentContext context, bool withPlace) => Select(context, withPlace, null);

        /// <summary>
        /// The same choice among the abilities of these ids only (a Use Any Ability's own set — step 90); null — among all.
        /// The draw is the agent's seeded random, through the abilities' Strategy.
        /// </summary>
        public string Select(AgentContext context, bool withPlace, IReadOnlyList<string> among)
        {
            _candidates.Clear();
            _scores.Clear();
            bool trace = context != null && context.TraceEnabled;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (!In(among, entry.Definition.Id) || !IsReady(entry, context) ||
                    (withPlace && !HasPlace(entry.Definition, context)))
                    continue;

                if (!TryScore(entry, context, out float score, out Consideration veto))
                {
                    if (trace)
                    {
                        context.Record(new DecisionRecord(context.LocalTime, DecisionKind.AbilityVetoed,
                            abilityId: entry.Definition.Id, detail: veto?.ToString()));
                    }

                    continue;
                }

                _candidates.Add(entry);
                _scores.Add(score);
            }

            PenaliseTheLastUsed();
            for (int i = 0; trace && i < _candidates.Count; i++)
            {
                context.Record(new DecisionRecord(context.LocalTime, DecisionKind.AbilityCandidate,
                    abilityId: _candidates[i].Definition.Id, score: _scores[i]));
            }

            ISelectionStrategy strategy = Strategy ?? DefaultStrategy;
            int chosen = _candidates.Count > 0 ? strategy.Select(_scores, context) : -1;
            if (chosen < 0)
                return null;

            LastSelectedScore = _scores[chosen];
            return _candidates[chosen].Definition.Id;
        }

        // The anti-repeat (handbook §4.6 — it varies the choice): the ability used last loses its share of its score, but
        // only while another of the candidates could be picked instead — with nothing else to pick, a penalty of 1 would
        // leave the agent unable to use its only ready ability ever again
        private void PenaliseTheLastUsed()
        {
            int last = -1;
            bool another = false;
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].Definition.Id == _lastUsedId)
                    last = i;
                else if (_scores[i] > 0f)
                    another = true;
            }

            if (last >= 0 && another)
                _scores[last] = Penalised(_candidates[last].Definition, _scores[last]);
        }

        private static float Penalised(AbilityDefinition definition, float score) =>
            Mathf.Max(0f, score * (1f - definition.RepeatPenalty));

        /// <summary>
        /// Something could be used right now: ready, not vetoed, scored above zero. Pure — no random, no trace — so a
        /// condition may ask it.
        /// </summary>
        public bool AnyAvailable(AgentContext context) => AnyAvailable(context, false);

        /// <summary>The same; withPlace — an ability that takes turns counts only with a place to be had in its pool.</summary>
        public bool AnyAvailable(AgentContext context, bool withPlace) => AnyAvailable(context, withPlace, null);

        /// <summary>
        /// The same among the abilities of these ids only (null — all). Pure: no trace, and no draw — an ability scored by
        /// a Random axis is scored with the numbers a pick made now would draw, and the agent's random is put back as it
        /// was (invariant 6: a question makes no state), so the answer is the pick's and asking twice answers the same.
        /// </summary>
        public bool AnyAvailable(AgentContext context, bool withPlace, IReadOnlyList<string> among)
        {
            ulong random = context?.RandomState ?? 0UL;
            try
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    if (In(among, _entries[i].Definition.Id) && IsReady(_entries[i], context) &&
                        (!withPlace || HasPlace(_entries[i].Definition, context)) &&
                        TryScore(_entries[i], context, out float score, out _) && score > 0f)
                        return true;
                }

                return false;
            }
            finally
            {
                if (context != null)
                    context.RandomState = random;
            }
        }

        private static bool In(IReadOnlyList<string> among, string id)
        {
            if (among == null)
                return true;

            for (int i = 0; i < among.Count; i++)
            {
                if (among[i] == id)
                    return true;
            }

            return false;
        }

        private static readonly ISelectionStrategy DefaultStrategy = new TopRandomSelection(2);

        /// <summary>Score of the ability the last Select returned (the trace records it without scoring again).</summary>
        public float LastSelectedScore { get; private set; }

        /// <summary>Cooldowns, start delays and "the ability has just ended" — once per step.</summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Runner is ITickedAbilityRunner ticked)
                    ticked.Tick(dt);

                if (entry.DelayLeft > 0f)
                    entry.DelayLeft = Mathf.Max(0f, entry.DelayLeft - dt);

                bool running = entry.Runner != null && entry.Runner.IsRunning;
                if (entry.WasRunning && !running)
                {
                    entry.CooldownLeft = entry.Definition.Cooldown;     // the pause counts from the END
                    entry.WasRunning = false;
                    continue;                                           // and starts whole, not a step shorter
                }

                entry.WasRunning = running;

                if (!running && entry.CooldownLeft > 0f)
                    entry.CooldownLeft = Mathf.Max(0f, entry.CooldownLeft - dt);     // never below zero: callers read what is left
            }
        }

        public void Cancel()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Runner != null && _entries[i].Runner.IsRunning)
                    _entries[i].Runner.Cancel();
            }
        }

        public void Clear()
        {
            _entries.Clear();
            _lastUsedId = null;
        }

        private Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Definition.Id == id)
                    return _entries[i];
            }

            return null;
        }

        private bool IsReady(Entry entry, AgentContext context) => BlockOf(entry, context) == AbilityBlock.None;

        /// <summary>What keeps the ability from being used right now (None — it is ready). For the debugger and the canvas.</summary>
        public AbilityBlock BlockOf(string id, AgentContext context) => BlockOf(Find(id), context);

        /// <summary>The same; withPlace — an ability that is ready but has no place in its pool now says TurnTaken.</summary>
        public AbilityBlock BlockOf(string id, AgentContext context, bool withPlace)
        {
            Entry entry = Find(id);
            AbilityBlock block = BlockOf(entry, context);
            return block == AbilityBlock.None && withPlace && !HasPlace(entry.Definition, context)
                ? AbilityBlock.TurnTaken
                : block;
        }

        /// <summary>
        /// The same; ignoreRest — the ability's own pause is not asked, nor whether it plays now (the graph keeps the
        /// pause itself — the Use Ability decision's Timer — and knows when it plays): everything else the ability asks
        /// is — the target, the ground, the place the agent must hold, the distance, the awareness, the line, the body,
        /// and withPlace a place in the ability's pool. What a decision filters by before it is taken (handbook §17).
        /// </summary>
        public AbilityBlock BlockOf(string id, AgentContext context, bool withPlace, bool ignoreRest)
        {
            Entry entry = Find(id);
            AbilityBlock block = BlockOf(entry, context, ignoreRest);
            return block == AbilityBlock.None && withPlace && !HasPlace(entry.Definition, context)
                ? AbilityBlock.TurnTaken
                : block;
        }

        /// <summary>Seconds until the ability has rested (its cooldown or its start delay, whichever is longer).</summary>
        public float RestLeft(string id)
        {
            Entry entry = Find(id);
            return entry != null ? Mathf.Max(entry.CooldownLeft, entry.DelayLeft) : 0f;
        }

        // The first thing that is not as the definition asks, in a fixed order: rest, the body, the target, the place
        // the agent must hold, the distance, the line. rested — judged as if it had rested and was not playing
        private AbilityBlock BlockOf(Entry entry, AgentContext context, bool rested = false)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Definition.Id))
                return AbilityBlock.Unknown;

            if (!rested && (entry.CooldownLeft > 0f || entry.DelayLeft > 0f))
                return AbilityBlock.Resting;

            if (!rested && entry.Runner != null && entry.Runner.IsRunning)
                return AbilityBlock.Playing;

            AbilityDefinition definition = entry.Definition;
            if (context != null)
            {
                if (definition.RequiresTarget && !context.HasTarget)
                    return AbilityBlock.NoTarget;

                if (definition.RequiresGround && context.Actuator != null && !context.Actuator.Body.IsGrounded)
                    return AbilityBlock.InAir;

                if (definition.RequiresPlace && !HoldsPlace(context))
                    return AbilityBlock.NeedsPlace;

                if (definition.RequiresTarget)
                {
                    float distance = context.DistanceToTarget;
                    if (distance < definition.Range.Min)
                        return AbilityBlock.TooNear;

                    if (distance > definition.Range.Max)
                        return AbilityBlock.TooFar;
                }

                if (definition.MinAwareness > 0f && context.TargetAwareness < definition.MinAwareness)
                    return AbilityBlock.Unaware;

                if (definition.RequiresTarget && definition.Angles != AbilityAngles.All && !OnItsSide(definition, context))
                    return AbilityBlock.WrongSide;

                if (definition.RequiresLineOfSight && !context.CanSeeTarget())
                    return AbilityBlock.NoLine;
            }

            return entry.Runner == null || entry.Runner.CanStart(context) ? AbilityBlock.None : AbilityBlock.BodyBusy;
        }

        // The target is where the ability wants it around the agent: by the flat angle from where the agent looks (its
        // object's forward) — in front within 45°, behind beyond 135°, at the side between
        private static bool OnItsSide(AbilityDefinition definition, AgentContext context)
        {
            Vector3 forward = context.Self != null ? context.Self.forward : Vector3.forward;
            Vector3 to = context.ToTarget;
            forward.y = 0f;
            to.y = 0f;
            if (to.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f)
                return (definition.Angles & AbilityAngles.Front) != 0;

            float angle = Vector3.Angle(forward, to);
            AbilityAngles side = angle <= 45f ? AbilityAngles.Front : angle >= 135f ? AbilityAngles.Back : AbilityAngles.Side;
            return (definition.Angles & side) != 0;
        }

        // The score as a choice among all the ready ones would see it: the anti-repeat only while another could be picked
        private float Score(Entry entry, AgentContext context)
        {
            if (!TryScore(entry, context, out float score, out _))
                return 0f;

            return entry.Definition.Id == _lastUsedId && AnotherReady(entry, context)
                ? Penalised(entry.Definition, score)
                : score;
        }

        // Another ability than this one is ready and scores above zero
        private bool AnotherReady(Entry except, AgentContext context)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != except && IsReady(_entries[i], context) &&
                    TryScore(_entries[i], context, out float score, out _) && score > 0f)
                    return true;
            }

            return false;
        }

        /// <summary>Weight × axes (the anti-repeat is the choice's: PenaliseTheLastUsed); false — a required axis had no data (veto names it).</summary>
        private static bool TryScore(Entry entry, AgentContext context, out float score, out Consideration veto)
        {
            score = 0f;
            veto = null;
            if (entry == null)
                return false;

            AbilityDefinition definition = entry.Definition;
            if (!UtilityMath.Evaluate(definition.Considerations, context, definition.Reduction,
                    out UtilityScore axes, out veto))
                return false;

            score = Mathf.Max(0f, definition.Weight * axes);
            return true;
        }
    }
}
