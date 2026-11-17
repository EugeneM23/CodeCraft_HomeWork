using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Puts down what the hands hold — at the feet, a little in front: the hands are free again (to strike, to take
    /// something else), and the thing lies there for anyone to pick up. Nothing held — nothing to do. Instant.
    /// (Taking a thing is the Interact block's: what an object gives is its own scenario — step 90.)
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Objects/Drop", "Task", "aikit.task.drop", Success = "Done", NeverFails = true, Simple = true)]
    public sealed class DropTask : AgentTask
    {
        /// <summary>Metres in front of the feet the thing is put down.</summary>
        public const float AHEAD = 0.6f;

        public override bool IsInstant => true;

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            Transform item = context?.Hands?.LetGo();
            if (item != null)
                Items.PutDown(item, Walking.Ground(context, context.Position + Walking.Forward(context) * AHEAD));
            return TaskStatus.Success;
        }
    }
}
