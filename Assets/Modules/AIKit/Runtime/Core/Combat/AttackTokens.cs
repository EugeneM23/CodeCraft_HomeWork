using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The attack tokens of one target (handbook §18.1–§18.2): pools by kind of attack, each with the target's capacity.
    /// An attacker files a request and keeps filing it every step it wants to attack; requests filed within the
    /// maturation window are ranked together — priority, then the one in melee range first, then the nearer, then the
    /// roster's order — never "who asked first" (frame order is not a rule). A request from farther than its pool's reach
    /// (WinRadius, or farther for a pool an ability reaches farther with — ReachFor) is refused at once (no token held
    /// for someone who cannot use it). A better request may take a token from a worse
    /// holder, but never from one whose ability has started (its wind-up runs to the end, §18.2.3). When a pool runs
    /// empty it rests a moment before the next attacker gets in. The tokens of a dead or gone attacker come back by
    /// themselves.
    /// </summary>
    public sealed class AttackTokens
    {
        private const float CLOSE_BONUS = 1000f;

        private sealed class Claim
        {
            public AICharacter Agent;
            public string Pool;
            public int Cost;
            public float Priority;
            public float Distance;
            public float Age;
            public bool Granted;
            public float FiledAt;
            public float AskedAt;
        }

        private readonly Engagement _engagement;
        private readonly List<Claim> _claims = new();
        private readonly List<Claim> _ranked = new();
        private readonly Stack<Claim> _spare = new();         // claims let go, filed again: asking anew allocates nothing
        private readonly Comparison<Claim> _compare;
        private readonly Dictionary<string, float> _rest = new();
        private readonly List<string> _restKeys = new();
        private float _clock;

        public AttackTokens(Engagement engagement)
        {
            _engagement = engagement;
            _compare = Compare;                        // made once: a sort every step allocates nothing
        }

        /// <summary>
        /// A request not asked again for this long is dropped: the one who filed it has gone on to something else (the
        /// node that asked was left). Asking is a refresh every step while waiting (handbook §18.1); a request left
        /// behind must not be granted later and take a place from those who still want it.
        /// </summary>
        public const float STALE_AFTER = 0.5f;

        /// <summary>Requests filed within this many seconds compete as one batch (0 — one step).</summary>
        public float MaturationWindow { get; set; }

        /// <summary>Farther than this a request is refused outright.</summary>
        public float WinRadius { get; set; } = 8f;

        /// <summary>A request from within this distance goes ahead of the queue (the one already in reach).</summary>
        public float CloseRange { get; set; } = 1.5f;

        /// <summary>Seconds a pool rests after its last token came back, before the next attacker gets one.</summary>
        public float RestAfterEmpty { get; set; }

        /// <summary>
        /// Files (or refreshes) a request of the agent for cost tokens of the pool; the answer is where it stands now —
        /// a granted token is held until Release.
        /// </summary>
        public TokenResult Request(AICharacter agent, string pool, int cost, float priority, float distance)
        {
            if (agent == null || string.IsNullOrEmpty(pool))
                return TokenResult.Refused;

            Claim claim = Find(agent, pool);
            if (claim != null && claim.Granted)
            {
                claim.Distance = distance;
                return TokenResult.Granted;
            }

            if (distance > ReachOf(pool) && !_nearestPools.ContainsKey(pool))   // "the nearer holds" sorts the far ones out itself
            {
                if (claim != null)
                    Discard(claim);
                return TokenResult.Refused;
            }

            if (claim == null)
            {
                claim = _spare.Count > 0 ? _spare.Pop() : new Claim();
                claim.Agent = agent;
                claim.Pool = pool;
                claim.FiledAt = _clock;
                _claims.Add(claim);
            }

            claim.Cost = Mathf.Max(1, cost);
            claim.Priority = priority;
            claim.Distance = distance;
            claim.AskedAt = _clock;
            return TokenResult.Pending;
        }

        public bool Holds(AICharacter agent, string pool) => Find(agent, pool) is { Granted: true };

        /// <summary>
        /// The agent waits for a token of the pool no longer (the node that asked was left): its request is dropped, so
        /// a token freed later goes to one who still wants it. A token already granted stays (Release gives it back).
        /// </summary>
        public void Withdraw(AICharacter agent, string pool)
        {
            Claim claim = Find(agent, pool);
            if (claim != null && !claim.Granted)
                Discard(claim);
        }

        // A claim goes from the list and is kept for the next request (a fresh one in all but the object)
        private void Discard(Claim claim)
        {
            _claims.Remove(claim);
            Recycle(claim);
        }

        private void Recycle(Claim claim)
        {
            claim.Agent = null;
            claim.Pool = null;
            claim.Granted = false;
            claim.Age = 0f;
            _spare.Push(claim);
        }

        private readonly HashSet<string> _firmPools = new();

        /// <summary>
        /// A token of this pool, once granted, is kept until it is released: a nearer request does not take it away.
        /// (By default a better request preempts a holder whose ability has not started — right for abilities, wrong for
        /// an attack that is a node of the graph: the holder is already on its way to strike and would strike anyway.)
        /// </summary>
        public void HoldFirmly(string pool) => _firmPools.Add(pool);

        private readonly Dictionary<string, float> _nearestPools = new();
        private readonly Dictionary<string, int> _capacities = new();
        private readonly Dictionary<string, float> _reaches = new();

        /// <summary>
        /// A token of this pool belongs to whoever is nearer to the target: a request nearer than a holder by more than
        /// the margin takes the farthest holder's token (the margin keeps two agents at the same distance from swapping
        /// every step). The distances are measured here every step, not taken from the requests — a holder does not ask.
        /// The one who lost the token finds it out with Holds.
        /// </summary>
        public void NearestHolds(string pool, float margin) => _nearestPools[pool] = Mathf.Max(0f, margin);

        /// <summary>
        /// The agent holds a place to attack the target from: a token of a pool where the nearer one holds (the
        /// places the node Take Attack Slot gives — of whatever group). True as well when the target gives out no
        /// such places at all: nothing limits the attack then.
        /// </summary>
        public bool HoldsPlace(AICharacter agent)
        {
            if (_nearestPools.Count == 0)
                return true;

            for (int i = 0; i < _claims.Count; i++)
            {
                Claim claim = _claims[i];
                if (claim.Granted && claim.Agent == agent && _nearestPools.ContainsKey(claim.Pool))
                    return true;
            }

            return false;
        }

        /// <summary>How many tokens the pool has, whatever the target says; 0 or less — back to the target's say.</summary>
        public void SetCapacity(string pool, int capacity)
        {
            if (capacity > 0)
                _capacities[pool] = capacity;
            else
                _capacities.Remove(pool);
        }

        /// <summary>
        /// The pool takes at most this many, whatever the target says — the strictest of those who name it counts:
        /// abilities that share a pool share its turns (a grenade of one at a time and a firebomb of two — one thrower at
        /// a time), whichever of them asked last. 0 or less — no say.
        /// </summary>
        public void LimitCapacity(string pool, int capacity)
        {
            if (string.IsNullOrEmpty(pool) || capacity <= 0)
                return;

            _capacities[pool] = _capacities.TryGetValue(pool, out int now) ? Mathf.Min(now, capacity) : capacity;
        }

        public int CapacityOf(string pool) =>
            _capacities.TryGetValue(pool, out int capacity) ? capacity : _engagement.CapacityOf(pool);

        /// <summary>
        /// A request for this pool is not refused from this far (the reach of what its tokens are for: an ability's range
        /// — one with no range part reaches from anywhere), nor nearer than WinRadius; the farthest any user names
        /// counts. The other pools keep WinRadius.
        /// </summary>
        public void ReachFor(string pool, float reach)
        {
            if (string.IsNullOrEmpty(pool) || reach <= WinRadius)
                return;

            _reaches[pool] = _reaches.TryGetValue(pool, out float now) ? Mathf.Max(now, reach) : reach;
        }

        /// <summary>How far from the target a request for this pool may come and not be refused.</summary>
        public float ReachOf(string pool) =>
            _reaches.TryGetValue(pool, out float reach) ? Mathf.Max(reach, WinRadius) : WinRadius;

        /// <summary>The agent could get a token now or soon: it holds one, or the pool has room and is not resting.</summary>
        public bool CouldWin(AICharacter agent, string pool) =>
            Holds(agent, pool) || (!IsResting(pool) && Used(pool) < CapacityOf(pool));

        /// <summary>The agent is done (or gives up asking): its token, or its request, is let go.</summary>
        public void Release(AICharacter agent, string pool)
        {
            Claim claim = Find(agent, pool);
            if (claim == null)
                return;

            bool granted = claim.Granted;
            Discard(claim);
            if (granted && Used(pool) == 0 && RestAfterEmpty > 0f)
                _rest[pool] = RestAfterEmpty;
        }

        /// <summary>
        /// Gives back every place to attack from that the agent holds here — of whatever group of places (the common one,
        /// the archers' own…): what Release Attack Slot does, so it needs no setting of its own.
        /// </summary>
        public void ReleasePlaces(AICharacter agent)
        {
            for (int i = _claims.Count - 1; i >= 0; i--)
            {
                Claim claim = _claims[i];
                if (claim.Agent == agent && _nearestPools.ContainsKey(claim.Pool))
                    Release(agent, claim.Pool);
            }
        }

        /// <summary>
        /// Everything of the agent goes (it died, left the world, its sensor lost the target). A loop, not RemoveAll with
        /// a lambda: a sensor calls it at every loss, and a step allocates nothing.
        /// </summary>
        public void Forget(AICharacter agent)
        {
            for (int i = _claims.Count - 1; i >= 0; i--)
            {
                Claim claim = _claims[i];
                if (claim.Agent != agent)
                    continue;

                _claims.RemoveAt(i);
                Recycle(claim);
            }
        }

        /// <summary>How many hold a pool's tokens (by tokens, not by heads).</summary>
        public int Used(string pool)
        {
            int used = 0;
            for (int i = 0; i < _claims.Count; i++)
            {
                if (_claims[i].Granted && _claims[i].Pool == pool)
                    used += _claims[i].Cost;
            }

            return used;
        }

        /// <summary>The holders of a pool, in grant order (debugging, the overlay, tests).</summary>
        public int HoldersOf(string pool, List<AICharacter> results)
        {
            results.Clear();
            for (int i = 0; i < _claims.Count; i++)
            {
                if (_claims[i].Granted && _claims[i].Pool == pool)
                    results.Add(_claims[i].Agent);
            }

            return results.Count;
        }

        /// <summary>The pools anybody asks for or holds now (debugging; allocates).</summary>
        public IEnumerable<string> Pools
        {
            get
            {
                var seen = new HashSet<string>();
                foreach (Claim claim in _claims)
                {
                    if (seen.Add(claim.Pool))
                        yield return claim.Pool;
                }
            }
        }

        public bool IsResting(string pool) => _rest.TryGetValue(pool, out float left) && left > 0f;

        /// <summary>Once per step (the world's): drop the gone, age the requests, grant the matured ones in rank order.</summary>
        public void Step(float dt)
        {
            _clock += dt;
            for (int i = _claims.Count - 1; i >= 0; i--)
            {
                Claim claim = _claims[i];
                bool gone = claim.Agent == null || !claim.Agent.isActiveAndEnabled;
                if (!gone && (claim.Granted || _clock - claim.AskedAt <= STALE_AFTER))
                    continue;

                _claims.RemoveAt(i);                    // its agent is gone, or nobody asks for it any more
                Recycle(claim);
            }

            if (_rest.Count > 0)
            {
                _restKeys.Clear();
                _restKeys.AddRange(_rest.Keys);
                foreach (string pool in _restKeys)
                    _rest[pool] -= dt;
            }

            MeasureNearestPools();

            // The window is ripe once its oldest request is old enough; then every request waiting competes at once
            _ranked.Clear();
            bool ripe = false;
            for (int i = 0; i < _claims.Count; i++)
            {
                Claim claim = _claims[i];
                if (claim.Granted)
                    continue;

                claim.Age += dt;
                ripe |= claim.Age >= MaturationWindow;
                _ranked.Add(claim);
            }

            if (!ripe || _ranked.Count == 0)
            {
                _ranked.Clear();
                return;
            }

            _ranked.Sort(_compare);
            for (int i = 0; i < _ranked.Count; i++)
            {
                Claim claim = _ranked[i];
                if (IsResting(claim.Pool))
                    continue;

                int capacity = CapacityOf(claim.Pool);
                bool nearest = _nearestPools.TryGetValue(claim.Pool, out float margin);
                bool mayPreempt = !nearest && !_firmPools.Contains(claim.Pool);
                if (Used(claim.Pool) + claim.Cost <= capacity || (mayPreempt && Preempt(claim, capacity))
                    || (nearest && TakeFromFarther(claim, capacity, margin)))
                    claim.Granted = true;
            }
        }

        // The pools where the nearer one holds: everybody's distance to the target as it is now
        private void MeasureNearestPools()
        {
            Transform target = _engagement.Target;
            if (_nearestPools.Count == 0 || target == null)
                return;

            for (int i = 0; i < _claims.Count; i++)
            {
                Claim claim = _claims[i];
                if (!_nearestPools.ContainsKey(claim.Pool))
                    continue;

                Vector3 offset = claim.Agent.transform.position - target.position;
                offset.y = 0f;
                claim.Distance = offset.magnitude;
            }
        }

        // The request is clearly nearer than the farthest holder: that holder's token goes to it
        private bool TakeFromFarther(Claim claim, int capacity, float margin)
        {
            Claim farthest = null;
            for (int i = 0; i < _claims.Count; i++)
            {
                Claim holder = _claims[i];
                // A holder in the middle of a blow or an ability keeps its place until it is over (handbook §18.2 rule 3)
                if (holder.Granted && holder.Pool == claim.Pool && !Started(holder.Agent) &&
                    (farthest == null || holder.Distance > farthest.Distance))
                    farthest = holder;
            }

            if (farthest == null || farthest.Distance <= claim.Distance + margin
                || Used(claim.Pool) - farthest.Cost + claim.Cost > capacity)
                return false;

            Discard(farthest);                          // it asks again when it wants the place back
            return true;
        }

        // A better request takes a token from the worst holder whose ability has not started yet
        private bool Preempt(Claim claim, int capacity)
        {
            Claim worst = null;
            for (int i = 0; i < _claims.Count; i++)
            {
                Claim holder = _claims[i];
                if (!holder.Granted || holder.Pool != claim.Pool || Started(holder.Agent))
                    continue;

                if (Compare(claim, holder) < 0 && (worst == null || Compare(holder, worst) > 0))
                    worst = holder;
            }

            if (worst == null || Used(claim.Pool) - worst.Cost + claim.Cost > capacity)
                return false;

            worst.Granted = false;                      // back in the queue; it was only walking up
            worst.Age = 0f;
            return true;
        }

        // The agent is at something that must run to its end: an ability that plays, or a blow that has started
        private static bool Started(AICharacter agent)
        {
            if (agent == null)
                return false;

            if (agent.Abilities.RunningPhase != AbilityPhase.None)
                return true;

            AgentState leaf = agent.Graph?.Current;
            return leaf != null && leaf.IsCommittedFor(agent.Context);
        }

        // Lower sorts first: higher score, then nearer, then the roster's order, then the earlier request
        private int Compare(Claim a, Claim b)
        {
            int byScore = Score(b).CompareTo(Score(a));
            if (byScore != 0)
                return byScore;

            int byDistance = a.Distance.CompareTo(b.Distance);
            if (byDistance != 0)
                return byDistance;

            int byOrder = _engagement.Roster.OrderOf(a.Agent).CompareTo(_engagement.Roster.OrderOf(b.Agent));
            return byOrder != 0 ? byOrder : a.FiledAt.CompareTo(b.FiledAt);
        }

        private float Score(Claim claim) => claim.Priority + (claim.Distance <= CloseRange ? CLOSE_BONUS : 0f);

        private Claim Find(AICharacter agent, string pool)
        {
            for (int i = 0; i < _claims.Count; i++)
            {
                if (_claims[i].Agent == agent && _claims[i].Pool == pool)
                    return _claims[i];
            }

            return null;
        }
    }
}
