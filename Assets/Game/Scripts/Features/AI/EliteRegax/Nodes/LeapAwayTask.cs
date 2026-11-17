using System;
using AIKit;
using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// Leap Away: jumps <see cref="MinDistance"/>..<see cref="MaxDistance"/> m away from the last blast (BlastPoint of
    /// the blackboard) onto ground it can land on, and lands. Needs a body that leaps (<see cref="ILeapActuator"/>).
    /// "Landed" — on the ground again; "No Room" — no body that leaps, no blast, or nowhere to land around.
    /// </summary>
    [Serializable]
    [AIKitNode("Game/Actions/Leap Away", "Task", "game.task.leap-away", Success = "Landed", Failure = "No Room",
        Simple = true)]
    public sealed class LeapAwayTask : AgentTask
    {
        [Min(0f)] public float MinDistance = 3f;
        [Min(0f)] public float MaxDistance = 5f;

        /// <summary>Seconds in the air.</summary>
        [Min(0.1f)] public float Duration = 0.45f;

        private sealed class Mem
        {
            public bool Started;
        }

        public override string Requirement => "away from BlastPoint";

        protected override void OnEnter(AgentContext context)
        {
            Memory<Mem>(context).Started = false;
            context?.Navigation?.Clear();
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context?.Actuator is not ILeapActuator leap)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            if (!m.Started)
            {
                Blackboard board = context.Blackboard;
                if (!board.TryGetKey(EliteBrain.BLAST_POINT, out BlackboardKey<Vector3> pointKey) || !board.Has(pointKey))
                    return TaskStatus.Failure;
                if (!leap.TryLeapAway(board.Get(pointKey), MinDistance, MaxDistance, Duration))
                    return TaskStatus.Failure;
                m.Started = true;
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Traversal))
                context.Actuator.Move(Vector3.zero);
            return leap.IsLeaping ? TaskStatus.Running : TaskStatus.Success;
        }
    }
}
