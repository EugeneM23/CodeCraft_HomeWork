namespace AIKit
{
    /// <summary>
    /// What leads an arrow on. A graph has four kinds of arrows and no others (the project's rule: the canvas is the
    /// program): the Brain's wire to a decision, a decision's Do to the first block of its chain (both Always), a
    /// block's output by how its work ended, and a block's On Tick pin. The numbers are kept: graph assets store them.
    /// </summary>
    public enum TransitionTrigger
    {
        /// <summary>A wire: the Brain's to a decision (the Brain chooses), a decision's Do (leads on at once).</summary>
        Always = 0,

        /// <summary>The block has finished, whatever the outcome.</summary>
        TasksFinished = 1,

        /// <summary>The block has finished and it worked (its "Done" output).</summary>
        TasksSucceeded = 4,

        /// <summary>The block has finished and it failed (its "Failed" output).</summary>
        TasksFailed = 5,

        /// <summary>
        /// The "On Tick" pin of a block the chain stays in: once per think, while the block runs, a chain of instant
        /// blocks runs from here and the flow comes back — the block is not left. Never fires as an arrow.
        /// </summary>
        Tick = 6
    }

    /// <summary>
    /// An arrow of the behavior graph: from the node that owns it to a target node, with what leads it on (its trigger)
    /// and, for a node that picks one of its numbered outputs (Random), the output it leaves from.
    /// </summary>
    [System.Serializable]
    public sealed class Transition
    {
        /// <summary>Id of the target node: what a saved graph keeps (the builder fills it from Target).</summary>
        public int TargetId;

        /// <summary>Where to go.</summary>
        [System.NonSerialized] public AgentState Target;

        public TransitionTrigger Trigger = TransitionTrigger.Always;

        /// <summary>
        /// The numbered output of its node the arrow leaves from (Random: output 0, 1, 2…); -1 — the node has no numbered
        /// outputs. Of a node that picks one output, only the arrow of the picked one may fire.
        /// </summary>
        public int Output = -1;

        /// <summary>Order it was added in: arrows of one node are asked in it.</summary>
        internal int Order { get; set; }

        /// <summary>The arrow leads on now: a wire always; an output when the owner's block has ended that way.</summary>
        public bool Check(AgentContext context, AgentState owner)
        {
            switch (Trigger)
            {
                case TransitionTrigger.Tick:
                    return false;                   // not an arrow: the On Tick chain is run by the graph itself

                case TransitionTrigger.TasksFinished:
                    return owner != null && owner.TasksFinishedFor(context);

                case TransitionTrigger.TasksSucceeded:
                    return owner != null && owner.TasksFinishedFor(context) && !owner.AnyTaskFailedFor(context);

                case TransitionTrigger.TasksFailed:
                    return owner != null && owner.TasksFinishedFor(context) && owner.AnyTaskFailedFor(context);

                default:
                    return true;
            }
        }

        /// <summary>The arrow's kind is one the graph knows (an old asset may hold a trigger that is gone).</summary>
        public bool IsKnownTrigger => Trigger is TransitionTrigger.Always or TransitionTrigger.TasksFinished or
            TransitionTrigger.TasksSucceeded or TransitionTrigger.TasksFailed or TransitionTrigger.Tick;

        public override string ToString() => $"{Trigger} → {(Target != null ? Target.DisplayName : "nowhere")}";
    }
}
