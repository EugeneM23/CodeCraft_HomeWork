using System;
using AIKit;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Search Last Seen: the target is lost (the senses are Searching: it was known and is out of sight, still
    /// remembered) and the agent is not yet where it saw it last. Its chain: Go To Last Seen. Holds without a target.
    /// </summary>
    [Serializable]
    [AIKitNode("Game/Decisions/Search Last Seen", "Task", "game.reasoner.search-last-seen", Simple = true,
        Endless = true)]
    public sealed class LastSeenReasoner : ReasonerTask
    {
        /// <summary>Metres from the last seen place at which it counts as reached.</summary>
        [Min(0f)] public float Arrive = 1.5f;

        public override bool NeedsTarget => false;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            AgentContext context = query.Context;
            if (!LastSeen(context, out Vector3 place))
            {
                whyNot = "nothing lost";
                return false;
            }

            Vector3 to = place - context.Position;
            to.y = 0f;
            if (to.magnitude <= Arrive)
            {
                whyNot = "searched there";
                return false;
            }

            whyNot = null;
            return true;
        }

        /// <summary>Where the lost target was seen last, while the senses search for it.</summary>
        public static bool LastSeen(AgentContext context, out Vector3 place)
        {
            place = default;
            AgentPerception perception = context?.Perception;
            if (perception == null || perception.Level != AlertLevel.Searching || perception.Best == null)
                return false;
            place = perception.Best.LastKnownPosition;
            return true;
        }
    }
}
