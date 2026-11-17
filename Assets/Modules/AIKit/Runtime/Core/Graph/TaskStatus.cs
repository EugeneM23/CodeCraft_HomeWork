namespace AIKit
{
    /// <summary>What a task reports after a step.</summary>
    public enum TaskStatus
    {
        /// <summary>Still working — tick it again next step.</summary>
        Running,

        Success,

        Failure
    }

    /// <summary>Why a task stopped: it tells OnExit whether to finish cleanly or to drop everything.</summary>
    public enum ExitReason
    {
        /// <summary>The task returned Success or Failure itself.</summary>
        Finished,

        /// <summary>Taken away before it was done: the Brain took a more important decision, or an arrow led on.</summary>
        Interrupted,

        /// <summary>The whole graph stopped (death, disable, scene unload).</summary>
        Stopped
    }
}
