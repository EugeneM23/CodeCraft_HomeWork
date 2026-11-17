using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>What a squad's decision is asked with: the squad it decides for and the Brain's target.</summary>
    public readonly struct SquadQuery
    {
        public readonly Squad Squad;

        /// <summary>The command graph's context: its blackboard (a flag wired into a decision).</summary>
        public readonly AgentContext Context;

        /// <summary>What the Squad Brain thinks about (its Target pin: what the members see).</summary>
        public readonly Transform Target;

        /// <summary>The squad acts on this decision now: a threshold with slack lets go only at its far end.</summary>
        public readonly bool IsCurrent;

        /// <summary>Somebody looks (the canvas in Play): a decision that does not hold writes out why, with its numbers.</summary>
        public readonly bool Explains;

        public SquadQuery(Squad squad, Transform target, bool isCurrent, bool explains, AgentContext context = null)
        {
            Squad = squad;
            Context = context;
            Target = target;
            IsCurrent = isCurrent;
            Explains = explains;
        }
    }

    /// <summary>
    /// A decision of a squad — wired to the Squad Brain of a command graph. It says only when it holds (in plain words,
    /// with its own settings); what the formation does then is the chain of squad blocks behind its Do pin (handbook §0.1:
    /// deciding and acting are layers apart). Wired to a character's Brain it never holds — the canvas shows the problem.
    /// </summary>
    [Serializable]
    public abstract class SquadDecisionTask : ReasonerTask
    {
        public sealed override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            Squad squad = query.Context?.Leads;
            if (squad == null)
            {
                whyNot = "a squad's decision: it belongs in a squad's command graph";
                return false;
            }

            return Holds(new SquadQuery(squad, query.Facts.Target, query.IsCurrent, query.Explains, query.Context), out whyNot);
        }

        /// <summary>The decision holds for the squad now; whyNot — the first thing that does not, with its value.</summary>
        public abstract bool Holds(in SquadQuery query, out string whyNot);

        public override void LeftCharge(AgentContext context) => context?.Leads?.MarkLeft(this);

        protected static bool Sees(in SquadQuery query, out string whyNot)
        {
            whyNot = query.Target != null ? null : "the squad sees nobody";
            return query.Target != null;
        }
    }

    /// <summary>Form Up: always holds — the squad's decision for when nothing more important does (give it the last priority).</summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Squad/Form Up", "Task", "aikit.reasoner.form-up", Simple = true, Endless = true, Order = 30)]
    public sealed class FormUpDecision : SquadDecisionTask
    {
        public override bool NeedsTarget => false;

        public override bool Holds(in SquadQuery query, out string whyNot)
        {
            whyNot = null;
            return true;
        }
    }

    /// <summary>
    /// Gather: the squad gathers. When it has been called together is a condition like any other, wired into its Condition
    /// pin: the flag of the command graph's blackboard the game sets (a bell, an order) — "Called is on". Its chain leads the
    /// gathering (Form Ring round the meeting place, with Stand For) and turns the flag off when it is over (Set), so the
    /// condition lets go and every member goes back to its own life. With no condition it always holds (15.11.2026, the
    /// user's word: every decision node has the same build — the Called pin is gone).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Squad/Gather", "Task", "aikit.reasoner.gather", Simple = true, Endless = true, Order = 20)]
    public sealed class GatherDecision : SquadDecisionTask
    {
        public override bool NeedsTarget => false;

        public override bool Holds(in SquadQuery query, out string whyNot)
        {
            whyNot = null;
            return true;
        }
    }

    /// <summary>Advance: the squad sees somebody (a foe far off, or one that ran: the pursuit is the ranks').</summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Squad/Advance", "Task", "aikit.reasoner.advance", Simple = true, Endless = true, Order = 40)]
    public sealed class AdvanceDecision : SquadDecisionTask
    {
        public override bool Holds(in SquadQuery query, out string whyNot) => Sees(query, out whyNot);
    }

    /// <summary>
    /// Surround: the foe is alone (nobody else the squad sees stands within Alone Within of it) and the squad has come up
    /// to it (Within of the nearest member). Once the squad stands in rings round it, it lets go only when the foe has
    /// broken out — Breaks Out metres beyond the outer ring — and is not taken again for Again After seconds: a foe that
    /// just broke out, still by the outer ring, would be surrounded at once (a crowd's chase, not the ranks').
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Squad/Surround", "Task", "aikit.reasoner.surround", Simple = true, Endless = true, Order = 50)]
    public sealed class SurroundDecision : SquadDecisionTask
    {
        /// <summary>The squad surrounds the foe when it is this near its nearest member, metres.</summary>
        [Min(0f)] public float Within = 5f;

        /// <summary>…and lets go of it when it is this far out of the outer ring.</summary>
        [Min(0f)] public float BreaksOut = 1.5f;

        /// <summary>The foe is alone: nobody else the squad sees stands this near it.</summary>
        [Min(0f)] public float AloneWithin = 8f;

        /// <summary>Seconds after the foe broke out before it is surrounded again.</summary>
        [Min(0f)] public float AgainAfter = 3f;

        public override bool Holds(in SquadQuery query, out string whyNot)
        {
            Squad squad = query.Squad;
            if (!Sees(query, out whyNot))
                return false;

            int together = squad.FoesNear(query.Target, AloneWithin);
            if (together > 1)
            {
                whyNot = query.Explains ? $"{together} foes together" : "not alone";
                return false;
            }

            if (query.IsCurrent && squad.Shape == SquadShape.Ring)
            {
                float outside = squad.OutOfRings(query.Target.position);
                if (outside > BreaksOut)
                {
                    whyNot = query.Explains ? $"broke out: {outside:0.0} m out of the rings" : "broke out";
                    return false;
                }

                return true;
            }

            float since = squad.SinceLeft(this);
            if (since < AgainAfter)
            {
                whyNot = query.Explains ? $"it broke out {since:0.0} s ago" : "it broke out";
                return false;
            }

            float distance = squad.DistanceTo(query.Target);
            if (distance > Within)
            {
                whyNot = query.Explains ? $"foe {distance:0.0} m, needs {Within:0.0} or closer" : "too far";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Hold Line (the decision): several foes stand together (at least two within Together Within of the target) and the squad has
    /// come up to them (Contact of the nearest member; it lets go beyond Let Go After) — a group against a group.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Squad/Hold Line", "Task", "aikit.reasoner.hold-the-line", Simple = true, Endless = true,
        Order = 60)]
    public sealed class HoldLineDecision : SquadDecisionTask
    {
        /// <summary>The squad holds the line when the foes are this near its nearest member, metres.</summary>
        [Min(0f)] public float Contact = 4f;

        /// <summary>…and lets go when they are farther than this.</summary>
        [Min(0f)] public float LetGoAfter = 7f;

        /// <summary>Foes are a group: at least two of them stand this near the target.</summary>
        [Min(0f)] public float TogetherWithin = 8f;

        public override bool Holds(in SquadQuery query, out string whyNot)
        {
            Squad squad = query.Squad;
            if (!Sees(query, out whyNot))
                return false;

            int together = squad.FoesNear(query.Target, TogetherWithin);
            if (together < 2)
            {
                whyNot = "the foe is alone";
                return false;
            }

            float distance = squad.DistanceTo(query.Target);
            float limit = query.IsCurrent ? Mathf.Max(Contact, LetGoAfter) : Contact;
            if (distance > limit)
            {
                whyNot = query.Explains ? $"foes {distance:0.0} m, needs {limit:0.0} or closer" : "too far";
                return false;
            }

            return true;
        }
    }
}
