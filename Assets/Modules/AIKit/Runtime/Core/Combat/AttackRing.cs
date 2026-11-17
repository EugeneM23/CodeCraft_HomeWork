using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The places around one target, as a ring the attackers share evenly (handbook §18.3: rings and space — they
    /// surround the target instead of crowding one side of it). Everyone who comes for the target asks for a place and
    /// gets an angle around it: two stand opposite, three at 120°, ten at 36° — the ring is cut into as many places as
    /// there are agents on it. The first one to come keeps the side it is already on and the rest are measured from it,
    /// so the ring does not spin under them; each of the others takes the free place nearest to where it stands, and
    /// keeps it until somebody joins or leaves.
    /// One ring per Engagement, with the fight's own clock (Step, from the roster): an agent that stops asking for a
    /// place lets it go FORGET seconds later, by that clock and not by its own — every agent of the fight is measured
    /// the same way, whenever it joined.
    /// The nodes that walk round a target (Chase, Flank, Square Off) ask it where to stand.
    /// </summary>
    public sealed class AttackRing
    {
        /// <summary>Seconds after an agent's last ask that its place is given up (it went on to something else).</summary>
        public const float FORGET = 1f;

        private readonly List<object> _agents = new();
        private readonly List<float> _bearings = new();
        private readonly List<float> _angles = new();
        private readonly List<float> _asked = new();
        /// <summary>
        /// Degrees the first attacker may move round the target before the places turn after it: a fighter's footwork
        /// as it swings does not make everybody shift (handbook §14.1 — hysteresis against shivering on a border).
        /// </summary>
        public const float ANCHOR_SLACK = 10f;

        private readonly List<int> _order = new();
        private readonly List<object> _round = new();
        private readonly System.Comparison<int> _roundTheTarget;
        private object _anchor;
        private float _anchorAt;
        private float _dealtFrom;
        private bool _redeal = true;
        private float _now;

        public AttackRing() => _roundTheTarget = RoundTheTarget;     // made once: a deal allocates nothing

        /// <summary>How many agents hold a place now.</summary>
        public int Count => _agents.Count;

        /// <summary>
        /// Somebody else than this agent anchors the ring now (the places are measured from it). Before anybody holds a
        /// place to attack from, the first to come anchors it and keeps it while it asks: the next one to come does not
        /// take the anchor over (the ring was dealt anew at every step of every one of them).
        /// </summary>
        public bool IsAnchoredByAnother(object agent) => _anchor != null && !ReferenceEquals(_anchor, agent);

        /// <summary>The angle between two places now: the ring cut into as many as there are agents.</summary>
        public float Gap => _agents.Count > 0 ? 360f / _agents.Count : 360f;

        /// <summary>The angle of this agent's place, or null when it holds none.</summary>
        public float? PlaceOf(object agent)
        {
            int at = _agents.IndexOf(agent);
            return at < 0 ? null : _angles[at];
        }

        /// <summary>
        /// The fight's clock, once per step (from the roster): those who stopped asking let their places go, and the
        /// rest share the circle anew.
        /// </summary>
        public void Step(float dt)
        {
            _now += dt;
            Forget();
        }

        /// <summary>
        /// The angle (degrees around the target, from world forward) this agent should stand at. fromTarget — where it
        /// stands now, as an offset from the target; anchor — this one is the first attacker: it keeps the side it is
        /// on and the rest are measured from it.
        /// </summary>
        public float AngleFor(object agent, Vector3 fromTarget, bool anchor = false)
        {
            float bearing = Bearing(fromTarget);
            if (anchor && !ReferenceEquals(_anchor, agent))
            {
                _anchor = agent;
                _redeal = true;
            }

            int at = _agents.IndexOf(agent);
            if (at >= 0)
            {
                _bearings[at] = bearing;
                _asked[at] = _now;
            }
            else
            {
                _agents.Add(agent);
                _bearings.Add(bearing);
                _angles.Add(bearing);
                _asked.Add(_now);
                at = _agents.Count - 1;
                _redeal = true;
            }

            if (_redeal)
                Share();
            else if (ReferenceEquals(_anchor, agent) && Mathf.Abs(Mathf.DeltaAngle(_anchorAt, bearing)) > ANCHOR_SLACK)
                Place(bearing);                 // the first attacker has gone round: the places turn after it, same order

            return _angles[at];
        }

        /// <summary>The agent gives its place up (it stopped attacking this target).</summary>
        public void Release(object agent)
        {
            int at = _agents.IndexOf(agent);
            if (at < 0)
                return;

            RemoveAt(at);
            Share();
        }

        /// <summary>Gives up the places of those who have not asked for FORGET seconds; true — somebody left.</summary>
        public bool Forget()
        {
            bool left = false;
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                if (_agents[i] == null || _now - _asked[i] > FORGET)
                {
                    RemoveAt(i);
                    left = true;
                }
            }

            if (left)
                Share();
            return left;
        }

        private void RemoveAt(int at)
        {
            if (ReferenceEquals(_anchor, _agents[at]))
                _anchor = null;

            _redeal = true;
            _agents.RemoveAt(at);
            _bearings.RemoveAt(at);
            _angles.RemoveAt(at);
            _asked.RemoveAt(at);
        }

        // The ring is cut into as many places as there are agents, measured from where the first one stands. The rest
        // are given their places in the order they stand round the target — the one next to the first attacker takes
        // the next place, and so on round the circle. Going round in order and not "the nearest free place to each"
        // is what keeps the share stable: nobody is sent across the circle past somebody else, nobody crosses anybody,
        // and when one more joins every place moves by at most one gap instead of being dealt anew.
        // The order is dealt only when somebody joins or leaves, or another agent leads. Between those the places only
        // turn after the first attacker: a crowd jostles, two waiting side by side step past each other, and dealing
        // the order anew then would hand each the other's place — both would turn round on the spot
        private void Share()
        {
            _redeal = false;
            _round.Clear();
            int count = _agents.Count;
            if (count == 0)
                return;

            int first = IndexOfLeader();
            float anchor = _bearings[first];
            _order.Clear();
            for (int i = 0; i < count; i++)
            {
                if (i != first)
                    _order.Add(i);
            }

            // round the target from the first attacker, counting the way the angles grow
            _dealtFrom = anchor;
            _order.Sort(_roundTheTarget);
            foreach (int i in _order)
                _round.Add(_agents[i]);
            Place(anchor);
        }

        private int RoundTheTarget(int a, int b) =>
            Round(_bearings[a] - _dealtFrom).CompareTo(Round(_bearings[b] - _dealtFrom));

        // The places from where the first attacker stands, in the order dealt
        private void Place(float anchor)
        {
            _anchorAt = anchor;
            int count = _agents.Count;
            if (count == 0)
                return;

            float gap = 360f / count;
            _angles[IndexOfLeader()] = anchor;
            for (int place = 0; place < _round.Count; place++)
                _angles[_agents.IndexOf(_round[place])] = Mathf.Repeat(anchor + gap * (place + 1), 360f);
        }

        // Who the places are measured from: the agent that said it leads, or the one that came first
        private int IndexOfLeader()
        {
            int first = _anchor != null ? _agents.IndexOf(_anchor) : -1;
            return first < 0 ? 0 : first;
        }

        private static float Round(float degrees) => Mathf.Repeat(degrees, 360f);

        // Where the agent stands, as an angle around the target from world forward
        private static float Bearing(Vector3 fromTarget)
        {
            fromTarget.y = 0f;
            return fromTarget.sqrMagnitude < 1e-6f ? 0f : Mathf.Repeat(Mathf.Atan2(fromTarget.x, fromTarget.z) * Mathf.Rad2Deg, 360f);
        }
    }
}
