using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// A node of the behavior graph: the Brain, a decision or a block of a decision's chain — one task each — and its
    /// arrows. The graph's invisible root is a node too: it holds the sensors (its background tasks) and every other
    /// node (Children), in one flat list.
    /// The node is a description shared by every agent that runs the graph; whether it is on, the time in it and whether
    /// its block is done are the agent's (IsActiveFor, TimeIn, TasksFinishedFor).
    /// </summary>
    [Serializable]
    [AIKitNode("State", "State", "aikit.state")]
    public sealed class AgentState : AgentNode
    {
        [System.NonSerialized] public List<AgentTask> Tasks = new();
        [System.NonSerialized] public List<Transition> Transitions = new();

        /// <summary>The nodes of the graph — the root's only (a graph is flat: no node holds nodes).</summary>
        [System.NonSerialized] public List<AgentState> Children = new();

        /// <summary>
        /// The root's only: the value nodes of the canvas no node reads yet (a Get put down before it is wired, a wire
        /// pulled out) — kept with the graph so the canvas does not lose them (step 90). A value node somebody reads is
        /// held by its readers instead. Nothing runs them: a value is only ever asked by a reader.
        /// </summary>
        [System.NonSerialized] public List<AgentValue> Values = new();

        /// <summary>One agent's run of the node.</summary>
        internal sealed class Run
        {
            public bool Active;
            public float Time;
            public bool TasksFinished;
            public bool[] TaskFinished;
        }

        public AgentState()
        {
        }

        public AgentState(string name = null) => Name = name;

        /// <summary>The graph's root for a node of the graph; null for the root itself.</summary>
        public AgentState Parent { get; internal set; }

        /// <summary>The node is on for the agent (the root, or the node the agent is in).</summary>
        public bool IsActiveFor(AgentContext context) => Record<Run>(context).Active;

        /// <summary>Seconds the agent has spent in the node since it entered it (from dt).</summary>
        public float TimeIn(AgentContext context) => Record<Run>(context).Time;

        /// <summary>
        /// Every foreground task has finished (a node without them finishes at once). Background tasks do not count.
        /// </summary>
        public bool TasksFinishedFor(AgentContext context) => Record<Run>(context).TasksFinished;

        /// <summary>A foreground task of the node ended in failure on its last run (asked once the tasks have finished).</summary>
        public bool AnyTaskFailedFor(AgentContext context)
        {
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (!Tasks[i].IsBackground && Tasks[i].StatusOf(context) == TaskStatus.Failure)
                    return true;
            }

            return false;
        }

        /// <summary>The node's block does its work at once (Set, Add, Branch, Debug Log, Random): it passes in one think.</summary>
        public bool IsInstant
        {
            get
            {
                bool any = false;
                for (int i = 0; i < Tasks.Count; i++)
                {
                    if (Tasks[i].IsBackground)
                        continue;
                    if (!Tasks[i].IsInstant)
                        return false;
                    any = true;
                }

                return any;
            }
        }

        /// <summary>The node picks one of its numbered outputs at random (it carries a Random task; null — it does not).</summary>
        public PickRandomTask Random
        {
            get
            {
                for (int i = 0; i < Tasks.Count; i++)
                {
                    if (Tasks[i] is PickRandomTask random)
                        return random;
                }

                return null;
            }
        }

        public bool HasForegroundTasks
        {
            get
            {
                for (int i = 0; i < Tasks.Count; i++)
                {
                    if (!Tasks[i].IsBackground)
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// The node's block repeats (AgentTask.Repeats — Strike: blow after blow): it is never done by itself — it runs
        /// again each time it ends while no arrow leads away from the output it ended through.
        /// </summary>
        public bool HasRepeatingTasks
        {
            get
            {
                for (int i = 0; i < Tasks.Count; i++)
                {
                    if (!Tasks[i].IsBackground && Tasks[i].Repeats)
                        return true;
                }

                return false;
            }
        }

        /// <summary>The node is the Brain: it chooses among the decisions wired to it (CombatBrainTask).</summary>
        public bool IsBrain
        {
            get
            {
                for (int i = 0; i < Tasks.Count; i++)
                {
                    if (Tasks[i] is CombatBrainTask)
                        return true;
                }

                return false;
            }
        }

        /// <summary>The decision this node is (a reasoner wired to a Brain); null — it is not one.</summary>
        public ReasonerTask Reasoner
        {
            get
            {
                for (int i = 0; i < Tasks.Count; i++)
                {
                    if (Tasks[i] is ReasonerTask reasoner)
                        return reasoner;
                }

                return null;
            }
        }

        /// <summary>A task of the node is at something that must run to its end for this agent (AgentTask.CommitsFor).</summary>
        public bool IsCommittedFor(AgentContext context)
        {
            Run run = Record<Run>(context);
            if (!run.Active)
                return false;

            bool[] flags = FlagsOf(run);
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (!flags[i] && !Tasks[i].IsBackground && Tasks[i].IsRunningFor(context) && Tasks[i].CommitsFor(context))
                    return true;
            }

            return false;
        }

        /// <summary>Adds a node to the graph (the root's list); returns this root.</summary>
        public AgentState AddChild(AgentState child)
        {
            if (child == null)
                return this;

            child.Parent = this;
            Children.Add(child);
            return this;
        }

        public AgentState AddTask(AgentTask task)
        {
            if (task != null)
                Tasks.Add(task);

            return this;
        }

        public Transition AddTransition(Transition transition)
        {
            if (transition == null)
                return null;

            transition.Order = Transitions.Count;
            Transitions.Add(transition);
            return transition;
        }

        internal void Enter(AgentContext context)
        {
            Run run = Record<Run>(context);
            run.Active = true;
            run.Time = 0f;
            ResetTasks(run);
        }

        /// <summary>
        /// The node's repeating block runs again (its next blow) or the node was entered again while it was on: the
        /// tasks run anew, but the node was never left, so the time in it keeps counting.
        /// </summary>
        internal void RestartTasks(AgentContext context) => ResetTasks(Record<Run>(context));

        private void ResetTasks(Run run)
        {
            run.TasksFinished = !HasForegroundTasks;
            bool[] flags = FlagsOf(run);
            for (int i = 0; i < flags.Length; i++)
                flags[i] = false;
        }

        internal void Exit(AgentContext context, ExitReason reason)
        {
            for (int i = 0; i < Tasks.Count; i++)
                Tasks[i].Interrupt(context, reason);

            Record<Run>(context).Active = false;
        }

        /// <summary>
        /// One step of the node: its unfinished tasks, every background task (entered again after it finishes), the time
        /// in it and the "all finished" flag, which only the foreground tasks decide.
        /// </summary>
        internal void Tick(AgentContext context, float dt)
        {
            Run run = Record<Run>(context);
            bool[] flags = FlagsOf(run);
            bool allFinished = true;
            for (int i = 0; i < Tasks.Count; i++)
            {
                if (flags[i])
                    continue;

                AgentTask task = Tasks[i];
                TaskStatus status = task.Tick(context, dt);
                if (task.IsBackground)
                    continue;               // never marked finished: entered again on the next step

                if (status == TaskStatus.Running)
                    allFinished = false;
                else
                    flags[i] = true;
            }

            run.TasksFinished = allFinished;
            run.Time += dt;
        }

        private bool[] FlagsOf(Run run)
        {
            if (run.TaskFinished == null || run.TaskFinished.Length != Tasks.Count)
                run.TaskFinished = new bool[Tasks.Count];

            return run.TaskFinished;
        }
    }
}
