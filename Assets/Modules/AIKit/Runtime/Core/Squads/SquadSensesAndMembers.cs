using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Squad Sight: the eyes of a squad's command graph — it writes into its variable what the squad goes for: the one of
    /// everything its members see (their Brains hand it in) nearest to the ranks, kept for a while and given up only for a
    /// clearly nearer one (handbook §21.2). Wire its variable into the Squad Brain's Target. Object Tag — only an object
    /// of this kind (and of the second one, if set); empty — whatever the members see. A squad has no eyes of its own:
    /// with no members seeing anything, it sees nothing.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Squad Sight", "Sensor", "aikit.sensor.squad-sight", Order = 90, WorksAlone = true)]
    public sealed class SquadSightTask : SensorTask
    {
        public SquadSightTask()
        {
            Tag = "";
            Interval = 0.1f;
        }

        // The squad's eyes hold no attack place of their own (step 90: SensorTask.GivesPlaceBack)
        protected override bool GivesPlaceBack => false;

        protected internal override float Reach(AgentContext context) => 0f;

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen) => false;

        private sealed class LookTimer
        {
            public float In;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null || string.IsNullOrEmpty(StoreIn))
                return TaskStatus.Running;

            LookTimer look = Memory<LookTimer>(context);
            look.In -= dt;
            if (look.In > 0f)
                return TaskStatus.Running;

            look.In = Mathf.Max(0f, Interval.Get(context));
            Transform focus = context.Leads?.Target;
            if (focus != null && !IsOfKind(focus))
                focus = null;
            context.Blackboard.Set(StoreIn, focus);
            return TaskStatus.Running;
        }

        // Of the sensor's kind: no kind set, or its tag (or the second one) — compared without reading the tag out (a read
        // makes a new string every look)
        private bool IsOfKind(Transform thing)
        {
            Type type = TypeLookedFor;
            if (type != null && !IsOfType(thing, type))
                return false;

            bool any = string.IsNullOrEmpty(Tag) || Tag == "Untagged";
            return any || thing.CompareTag(Tag) || HasAlsoTag && thing.CompareTag(AlsoTag);
        }
    }

    /// <summary>
    /// Follow Squad: a character's decision — the character marches in a squad (its AI Squad lists it) and the squad's
    /// command graph leads a formation now: keep your slot (its chain: Keep Formation, In Reach → Strike, back). Give the
    /// character's own decisions that matter more a more important priority — Kite when a foe comes close, Climb, Fetch a
    /// rock — and it leaves the ranks for them and comes back to its slot when they let go (handbook §20a.2: the ranks
    /// are no priority over staying alive). The squad moves the formation only; the character moves itself.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Squad/Follow Squad", "Task", "aikit.reasoner.follow-squad", Simple = true, Endless = true,
        Order = 10)]
    public sealed class FollowSquadReasoner : ReasonerTask
    {
        public override bool NeedsTarget => false;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            Squad squad = query.Context?.Squad;
            if (squad == null || !squad.Has(query.Context))
            {
                whyNot = "in no squad";
                return false;
            }

            if (!squad.IsLed)
            {
                whyNot = "the squad leads no formation";
                return false;
            }

            whyNot = null;
            return true;
        }
    }
}
