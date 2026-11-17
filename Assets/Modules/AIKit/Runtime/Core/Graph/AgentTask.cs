using Unity.Profiling;

namespace AIKit
{
    /// <summary>
    /// An action that takes time: entered once, ticked while it reports Running, exited once. Tasks of a state run in
    /// parallel while the state is active; a finished task is not ticked again until the state is entered anew (a background task is: see IsBackground).
    /// Time comes only from dt, so a task behaves the same in a test with manual steps.
    /// A task is shared by every agent that runs its graph: whatever it keeps between ticks goes into Memory&lt;T&gt;
    /// (a small class of fields, one per agent), never into a field of the task.
    /// </summary>
    [System.Serializable]
    public abstract class AgentTask : AgentNode
    {
        /// <summary>One agent's run of the task: the base's flags and the task's own memory.</summary>
        internal sealed class Run
        {
            public bool Running;
            public TaskStatus Status = TaskStatus.Running;
            public object Own;
        }

        /// <summary>The task is between OnEnter and OnExit for this agent.</summary>
        public bool IsRunningFor(AgentContext context) => Record<Run>(context).Running;

        /// <summary>
        /// A background task runs for as long as its state is active: after it finishes it is entered again on the next
        /// step (aiming, facing, looking around), and it does not count when the state asks whether its tasks are done.
        /// </summary>
        public bool IsBackground;

        /// <summary>
        /// What the block requires or does by itself, as part of what it is, written on its node in plain words (step
        /// 90): "only with a weapon that shoots" (Kite), "needs a climbing body" (Climb), "asks for an attack place while
        /// it waits" (Square Off). The canvas draws it under the node's settings — the canvas is the program, so what a
        /// block does that no wire shows is said here. Null — nothing to say.
        /// </summary>
        public virtual string Requirement => null;

        /// <summary>
        /// The task works in whole steps and has no "done" (an attack: blow after blow): its node runs the task again each
        /// time it ends while no arrow leads away from the output it ended through — the agent stays in the node. A step
        /// that has started runs to its end: the Brain takes another decision only between the steps.
        /// </summary>
        public virtual bool Repeats => false;

        /// <summary>
        /// The task does its work at once and is done on its first tick (set a variable, branch, log). Only such
        /// nodes go into the On Tick chain of a node the flow stays in: they run and the flow comes back.
        /// </summary>
        public virtual bool IsInstant => false;

        /// <summary>
        /// The task only asks (a Branch): it changes nothing a question could read, so the answers kept for the think stay
        /// after it. Any other instant task has done something when it is done — the questions after it are answered anew
        /// (AgentContext.ForgetAnswers).
        /// </summary>
        protected internal virtual bool OnlyAsks => false;

        /// <summary>
        /// The task is one action — it is started once and runs out by itself (use an ability, call for help), there
        /// is nothing to do "every think" while it lasts. Such a node has no On Tick pin and no loop mark.
        /// </summary>
        public virtual bool IsOneShot => IsInstant;

        /// <summary>
        /// A repeating task is in the middle of a step for this agent right now — the default: for as long as it
        /// runs. One that may also wait between its steps (a fighter with nothing ready) says no while it waits, and
        /// the Brain is free to take another decision then.
        /// </summary>
        public virtual bool IsMidStepFor(AgentContext context) => true;

        /// <summary>
        /// The task is at something that must run to its end for this agent — a blow, an ability: a reasoner (Combat
        /// Brain) does not take the agent to another action meanwhile. By default: a repeating task in a step.
        /// </summary>
        public virtual bool CommitsFor(AgentContext context) => Repeats && IsMidStepFor(context);

        /// <summary>What the agent's last tick of the task returned (Running while working).</summary>
        public TaskStatus StatusOf(AgentContext context) => Record<Run>(context).Status;

        /// <summary>The task's memory for this agent (made on first ask, kept between runs: reset it in OnEnter).</summary>
        protected T Memory<T>(AgentContext context) where T : class, new()
        {
            Run run = Record<Run>(context);
            if (run.Own is not T own)
            {
                own = new T();
                run.Own = own;
            }

            return own;
        }

        public TaskStatus Tick(AgentContext context, float dt)
        {
            Run run = Record<Run>(context);
            if (!run.Running)
            {
                run.Running = true;
                run.Status = TaskStatus.Running;
                OnEnter(context);
            }

            TaskStatus status = TickMeasured(context, dt);
            run.Status = status;
            if (IsInstant && !OnlyAsks)
                context?.ForgetAnswers();           // its work is done: the questions after it see what it changed
            if (status == TaskStatus.Running)
                return status;

            run.Running = false;
            OnExit(context, ExitReason.Finished);
            context?.Arbiter.Release(this);
            return status;
        }

        /// <summary>Taken away from the outside: the task drops what it holds and does not report a result.</summary>
        public void Interrupt(AgentContext context, ExitReason reason = ExitReason.Interrupted)
        {
            Run run = Record<Run>(context);
            if (!run.Running)
                return;

            run.Running = false;
            run.Status = TaskStatus.Failure;
            OnExit(context, reason);
            context?.Arbiter.Release(this);
        }

        /// <summary>
        /// Claims an axis of the body for this task (handbook §14.3) — true: write it this tick; false: a stronger writer
        /// holds it, write nothing. Without a context there is nobody to argue with. Released when the task exits.
        /// </summary>
        protected bool Claim(AgentContext context, BodyAxis axis, MoveAuthority authority) =>
            context == null || context.Arbiter.TryClaim(this, axis, authority);

        protected virtual void OnEnter(AgentContext context)
        {
        }

        /// <summary>
        /// OnTick inside a profiler sample named after the task class (one sample per kind of task shows which one is
        /// expensive) and, in the editor, under the task's name for the axis write detector.
        /// </summary>
        private TaskStatus TickMeasured(AgentContext context, float dt)
        {
#if UNITY_EDITOR
            AgentNode previousWriter = AxisWriteDetector.CurrentWriter;
            AxisWriteDetector.CurrentWriter = this;
#endif
#if ENABLE_PROFILER
            if (!_hasMarker)
            {
                _marker = new ProfilerMarker(GetType().Name);
                _hasMarker = true;
            }

            _marker.Begin();
#endif
            try
            {
                return OnTick(context, dt);
            }
            finally
            {
#if ENABLE_PROFILER
                _marker.End();
#endif
#if UNITY_EDITOR
                AxisWriteDetector.CurrentWriter = previousWriter;
#endif
            }
        }

#if ENABLE_PROFILER
        [System.NonSerialized] private ProfilerMarker _marker;
        [System.NonSerialized] private bool _hasMarker;
#endif

        protected abstract TaskStatus OnTick(AgentContext context, float dt);

        protected virtual void OnExit(AgentContext context, ExitReason reason)
        {
        }
    }
}
