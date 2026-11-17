using System;

namespace AIKit
{
    /// <summary>
    /// Climb: the target is where only climbing leads — up on a wall the character cannot walk onto, or down below the
    /// wall it stands on. The body is asked (handbook §13.2: what an edge asks is the body's): a body that does not
    /// climb never takes it. Its chain is the Climb To node, which opens the walls to the character's paths while it
    /// runs. Holds while the body is on a wall, so a climb is never left halfway. Asking "only by climbing?" plans two
    /// paths, so the body keeps its answer a little while (half a second). The canvas says on the node what it needs
    /// (step 90, 10.2): a body that climbs walls.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Movement/Climb", "Task", "aikit.reasoner.climb", Simple = true, Endless = true, Order = 50)]
    public sealed class ClimbReasoner : ReasonerTask
    {
        /// <summary>What it asks of the character by itself, written on the node (10.2): a body that climbs.</summary>
        public override string Requirement => "needs a body that climbs walls";

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            TargetFacts facts = query.Facts;
            if (!Reasons.Sees(facts, out whyNot))
                return false;

            if (query.Context?.Navigation is not IClimbingNavigator { CanClimb: true } climber)
            {
                whyNot = "the body does not climb";
                return false;
            }

            if (query.IsCurrent && climber.ClimbDirection != 0)
                return true;                            // on a wall: the climb goes on to its end

            switch (climber.WayTo(facts.Target.position))
            {
                case ClimbWay.OnlyByClimbing:
                    return true;
                case ClimbWay.OnFoot:
                    whyNot = "the target can be reached on foot";
                    return false;
                default:
                    whyNot = "no way to the target, even climbing";
                    return false;
            }
        }
    }
}
