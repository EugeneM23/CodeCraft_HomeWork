using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Daily Routine: always holds — what a character does when nothing more important does: its day of work, talks,
    /// meals, in a chain that leads round to its first step (handbook B.6: a living world runs on schedules). Give it the
    /// last priority.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Life/Daily Routine", "Task", "aikit.reasoner.daily-routine", Simple = true, Endless = true, Order = 10)]
    public sealed class DailyRoutineReasoner : ReasonerTask
    {
        public override bool NeedsTarget => false;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            whyNot = null;
            return true;
        }
    }

    /// <summary>
    /// Sleep: the night's decision — its chain takes the character home (Go Inside). When it is night is a condition like any
    /// other, wired into its Condition pin: a Compare "Night is there" of the variable a Time Of Day sensor writes (it "sees"
    /// the clock for the hours of the night). In the morning the sensor lets go, the condition does too, and the Brain takes
    /// the day again. With no condition it always holds (15.11.2026, the user's word: every decision node has the same
    /// build — the Night pin is gone).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Life/Sleep", "Task", "aikit.reasoner.sleep", Simple = true, Endless = true, Order = 20)]
    public sealed class SleepReasoner : ReasonerTask
    {
        public override bool NeedsTarget => false;

        public override bool Holds(in ReasonerQuery query, out string whyNot)
        {
            whyNot = null;
            return true;
        }
    }
}
