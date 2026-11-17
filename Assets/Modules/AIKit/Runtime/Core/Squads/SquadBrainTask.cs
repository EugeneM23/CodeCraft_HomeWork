using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The Squad Brain: the Brain of a squad's command graph — the graph an AI Squad runs once for the whole squad
    /// (handbook §21: the coordinator decides for the group). It chooses among the squad's decisions wired to it (Form
    /// Up, Advance, Surround, Hold Line) the way a character's Brain does, and the chosen decision's chain of squad
    /// blocks leads the formation (March To Target, Form Ring, Hold Line, Return To Banner). It moves only the formation:
    /// each member's own graph decides whether it keeps its slot (Follow Squad) or does something of its own — kites,
    /// climbs — and comes back (§20a.2: the ranks are no priority over staying alive; §22.1: the member's own behaviour
    /// plus what only the squad knows). Its Target is what the squad's members see (Squad Sight writes it); the settings
    /// are the formation's: Files a rank, Spacing between files, Rank Gap between ranks, and how many may strike one foe
    /// at once (§18: tokens).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Brain/Squad Brain", "Task", "aikit.task.squad-brain", Simple = true, Endless = true, Order = 20)]
    public sealed class SquadBrainTask : CombatBrainTask
    {
        /// <summary>How many stand in a rank.</summary>
        [Min(1)] public int Files = 10;

        /// <summary>Metres between two of a rank.</summary>
        [Min(0.5f)] public float Spacing = 1.8f;

        /// <summary>Metres between two ranks.</summary>
        [Min(0.5f)] public float RankGap = 2f;

        /// <summary>How many of the squad strike one foe at once.</summary>
        [Min(1)] public int StrikersPerFoe = 3;

        // The squad takes the formation's numbers every think; the Brain thinks about what its Target pin brings
        protected override Transform ThinkAbout(AgentState brain, AgentContext context, Transform seen)
        {
            context?.Leads?.Configure(Files, Spacing, RankGap, StrikersPerFoe);
            return seen;
        }
    }
}
