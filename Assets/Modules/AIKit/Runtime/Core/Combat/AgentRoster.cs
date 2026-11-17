using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>A target's say on how many may attack it at once (optional, on the target): per token pool.</summary>
    public interface ITokenCapacity
    {
        int CapacityOf(string pool);
    }

    /// <summary>What a token request came to.</summary>
    public enum TokenResult
    {
        /// <summary>The token is held: attack.</summary>
        Granted,

        /// <summary>The request is filed and waits (the window is maturing or the pool is full): hold the band.</summary>
        Pending,

        /// <summary>No chance now (out of the radius where it can be won, no engagement): do something else.</summary>
        Refused
    }

    /// <summary>
    /// The agents of a world in a fixed order (handbook §21.4): the order requests are ranked in, the one place allies
    /// are found ("an ally in front" is a roster query — an agent never reads another agent, invariant 1), and the
    /// owner of the engagements — one per target, each with its attack tokens. An instance, never a static: a level
    /// owns its roster through its world.
    /// </summary>
    public sealed class AgentRoster
    {
        private readonly List<AICharacter> _members = new();
        private readonly Dictionary<Transform, Engagement> _engagements = new();
        private readonly List<Transform> _ended = new();

        public IReadOnlyList<AICharacter> Members => _members;

        public int Count => _members.Count;

        /// <summary>The fights round the targets (a foreach over it allocates nothing: Release Attack Slot, an ability's token).</summary>
        public Dictionary<Transform, Engagement>.ValueCollection Engagements => _engagements.Values;

        public void Add(AICharacter agent)
        {
            if (agent != null && !_members.Contains(agent))
                _members.Add(agent);
        }

        /// <summary>The agent leaves: every token it holds or asked for is let go.</summary>
        public void Remove(AICharacter agent)
        {
            if (agent == null)
                return;

            _members.Remove(agent);
            foreach (Engagement engagement in _engagements.Values)
                engagement.Tokens.Forget(agent);
        }

        public int OrderOf(AICharacter agent) => _members.IndexOf(agent);

        /// <summary>The engagement around a target (made on first ask); null for no target.</summary>
        public Engagement EngagementOf(Transform target)
        {
            if (target == null)
                return null;

            if (!_engagements.TryGetValue(target, out Engagement engagement))
            {
                engagement = new Engagement(this, target);
                _engagements[target] = engagement;
            }

            return engagement;
        }

        /// <summary>
        /// The engagement around a target if one is there — none is made: a question (does it hold a place, who attacks
        /// first) must not make what it asks about, or every target merely seen would be stepped as a fight from then on.
        /// Null — nobody has asked for anything round it yet, or no target.
        /// </summary>
        public Engagement FindEngagement(Transform target) =>
            target != null && _engagements.TryGetValue(target, out Engagement engagement) ? engagement : null;

        /// <summary>Once per step: requests mature, the tokens of the dead and the gone come back, dead targets end.</summary>
        public void Step(float dt)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                if (_members[i] == null)
                    _members.RemoveAt(i);
            }

            _ended.Clear();
            foreach (KeyValuePair<Transform, Engagement> pair in _engagements)
            {
                if (!pair.Value.IsAlive)
                {
                    _ended.Add(pair.Key);
                }
                else
                {
                    pair.Value.Tokens.Step(dt);
                    pair.Value.Ring.Step(dt);
                }
            }

            foreach (Transform target in _ended)
                _engagements.Remove(target);
        }

        /// <summary>
        /// Another member within distance in front of the given one, along direction (a corridor as wide as the
        /// body): the waiting-behind rule of navigation, asked of the roster instead of the physics.
        /// </summary>
        public bool AllyAhead(AICharacter self, Vector3 from, Vector3 direction, float distance)
        {
            if (direction.sqrMagnitude < 1e-6f)
                return false;

            Vector3 along = direction.normalized;
            for (int i = 0; i < _members.Count; i++)
            {
                AICharacter other = _members[i];
                if (other == null || other == self || !other.gameObject.activeInHierarchy || other.Context == null)
                    continue;

                AgentBodyInfo body = other.Context.Actuator != null ? other.Context.Actuator.Body : AgentBodyInfo.Default;
                Vector3 offset = body.Position - from;
                float ahead = Vector3.Dot(offset, along);
                if (ahead <= 0f || ahead > distance + body.Radius)
                    continue;

                Vector3 aside = offset - along * ahead;
                if (aside.magnitude <= Mathf.Max(body.Height, body.Radius * 2f))
                    return true;
            }

            return false;
        }

        /// <summary>The roster as one agent's IAllyProbe.</summary>
        public IAllyProbe ProbeFor(AICharacter agent) => new AllyView(this, agent);

        private sealed class AllyView : IAllyProbe
        {
            private readonly AgentRoster _roster;
            private readonly AICharacter _self;

            public AllyView(AgentRoster roster, AICharacter self)
            {
                _roster = roster;
                _self = self;
            }

            public bool AllyAhead(Vector3 from, Vector3 direction, float distance) =>
                _roster.AllyAhead(_self, from, direction, distance);
        }
    }

    /// <summary>A target and everything about fighting it: its attack tokens.</summary>
    public sealed class Engagement
    {
        public Engagement(AgentRoster roster, Transform target)
        {
            Roster = roster;
            Target = target;
            Tokens = new AttackTokens(this);
        }

        public AgentRoster Roster { get; }

        public Transform Target { get; }

        public AttackTokens Tokens { get; }

        /// <summary>Where the agents of this fight stand around the target: a place of their own for each.</summary>
        public AttackRing Ring { get; } = new();

        /// <summary>
        /// The target is still there and still a target: not destroyed or pooled, perceivable by its own say, and not
        /// rejected by the game's rules the agents carry (an ITargetFilter — "only the living" turns a corpse away).
        /// </summary>
        public bool IsAlive
        {
            get
            {
                if (Target == null || !Target.gameObject.activeInHierarchy)
                    return false;

                if (Target.TryGetComponent(out IPerceivable perceivable) && !perceivable.IsPerceivable)
                    return false;

                IReadOnlyList<AICharacter> members = Roster.Members;
                for (int i = 0; i < members.Count; i++)
                {
                    ITargetFilter filter = members[i] != null ? members[i].Context?.TargetFilter : null;
                    if (filter != null && !filter.Accepts(Target))
                        return false;
                }

                return true;
            }
        }

        /// <summary>How many may hold a pool's tokens at once: the target's say, else one per three of the roster.</summary>
        public int CapacityOf(string pool) => CapacityOf(Target, Roster, pool);

        /// <summary>
        /// The same for a target nobody has asked about yet (no engagement is made to know it — a question makes no
        /// state): the target's say, else one per three of the roster.
        /// </summary>
        public static int CapacityOf(Transform target, AgentRoster roster, string pool)
        {
            ITokenCapacity capacity = target != null ? target.GetComponentInChildren<ITokenCapacity>() : null;
            if (capacity != null)
                return Mathf.Max(0, capacity.CapacityOf(pool));

            return RoleAssignment.AttackersFor(roster?.Count ?? 0);
        }
    }

    /// <summary>The minimum of role assignment: how many attackers a fight of n agents gets (a third, at least one).</summary>
    public static class RoleAssignment
    {
        public static int AttackersFor(int agents) => Mathf.Max(1, Mathf.CeilToInt(agents / 3f));
    }
}
