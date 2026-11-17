using System;

namespace AIKit
{
    public enum DecisionKind
    {
        /// <summary>The graph started: the first leaf was selected.</summary>
        Start,

        /// <summary>An edge fired: From → To through Transition.</summary>
        Transition,

        /// <summary>Not recorded since step 89 (a finished chain hands the choice to the Brain: a Transition); kept for old traces.</summary>
        Reselect,

        /// <summary>An ability that passed its conditions, with its score (one record per candidate).</summary>
        AbilityCandidate,

        /// <summary>The ability that was used, with its score.</summary>
        AbilityUsed,

        /// <summary>A state was entered directly (EnterState: a load, a debug jump): From → To.</summary>
        Entered,

        /// <summary>An ability was not considered: a required axis had no data (Detail names it). Not a zero score.</summary>
        AbilityVetoed
    }

    /// <summary>
    /// One decision of an agent as data: no string is built when it is recorded, so tracing costs almost nothing and two
    /// traces can be compared field by field (the determinism tests do exactly that). ToString formats it for people.
    /// </summary>
    public readonly struct DecisionRecord : IEquatable<DecisionRecord>
    {
        public readonly float Time;
        public readonly DecisionKind Kind;
        public readonly AgentState From;
        public readonly AgentState To;
        public readonly Transition Transition;
        public readonly string AbilityId;
        public readonly float Score;

        /// <summary>What else the decision needs said: the axis that vetoed an ability.</summary>
        public readonly string Detail;

        public DecisionRecord(float time, DecisionKind kind, AgentState from = null, AgentState to = null,
            Transition transition = null, string abilityId = null, float score = 0f, string detail = null)
        {
            Detail = detail;
            Time = time;
            Kind = kind;
            From = from;
            To = to;
            Transition = transition;
            AbilityId = abilityId;
            Score = score;
        }

        public bool Equals(DecisionRecord other) =>
            Time.Equals(other.Time) && Kind == other.Kind && Equals(From?.DisplayName, other.From?.DisplayName)
            && Equals(To?.DisplayName, other.To?.DisplayName) && AbilityId == other.AbilityId
            && Score.Equals(other.Score) && Detail == other.Detail;

        public override bool Equals(object obj) => obj is DecisionRecord other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Time, (int)Kind, AbilityId, Score);

        // A node as the trace names it: what it is called, its id, and its type when it is called otherwise (step 90)
        public override string ToString() =>
            Kind switch
            {
                DecisionKind.Start => $"{Time:0.00}  start → {NodeTitles.Traced(To)}",
                DecisionKind.Transition =>
                    $"{Time:0.00}  {NodeTitles.Traced(From)} ─{Transition?.Trigger}→ {NodeTitles.Traced(To)}",
                DecisionKind.Reselect => $"{Time:0.00}  {NodeTitles.Traced(From)} done → {NodeTitles.Traced(To)}",
                DecisionKind.AbilityVetoed => $"{Time:0.00}    vetoed {AbilityId} by {Detail}",
                DecisionKind.Entered => $"{Time:0.00}  {NodeTitles.Traced(From)} entered → {NodeTitles.Traced(To)}",
                DecisionKind.AbilityCandidate => $"{Time:0.00}    candidate {AbilityId} {Score:0.00}",
                _ => $"{Time:0.00}  ability {AbilityId} (score {Score:0.00})"
            };
    }

    /// <summary>The last Capacity decisions of an agent, oldest first; a ring buffer — recording never allocates.</summary>
    public sealed class DecisionTrace
    {
        public const int DEFAULT_CAPACITY = 128;

        private readonly DecisionRecord[] _records;
        private int _start;

        public DecisionTrace(int capacity = DEFAULT_CAPACITY) =>
            _records = new DecisionRecord[Math.Max(1, capacity)];

        public int Capacity => _records.Length;

        public int Count { get; private set; }

        /// <summary>How many records were ever added (older ones are overwritten).</summary>
        public int TotalRecorded { get; private set; }

        /// <summary>0 — the oldest kept record.</summary>
        public DecisionRecord this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return _records[(_start + index) % _records.Length];
            }
        }

        public DecisionRecord Last => Count > 0 ? this[Count - 1] : default;

        public void Add(in DecisionRecord record)
        {
            TotalRecorded++;
            if (Count < _records.Length)
            {
                _records[(_start + Count) % _records.Length] = record;
                Count++;
                return;
            }

            _records[_start] = record;
            _start = (_start + 1) % _records.Length;
        }

        public void Clear()
        {
            Count = 0;
            _start = 0;
        }
    }
}
