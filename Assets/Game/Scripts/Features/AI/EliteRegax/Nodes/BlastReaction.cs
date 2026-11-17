using System;
using AIKit;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Blast Nearby: a blast went off close to the agent (the game counts it in the agent's blackboard: BlastCount,
    /// BlastPoint, BlastTime — <see cref="EliteBrain"/>), taken out of turn like every reaction. Each blast is a new
    /// event (the count grows); it is answered within <see cref="Window"/> seconds of it. Its chain: a short stun, then
    /// Leap Away from the blast point. Holds without a target: a blast is felt whoever set it off.
    /// </summary>
    [Serializable]
    [AIKitNode("Game/Decisions/Blast Nearby", "Task", "game.reasoner.blast-nearby", Simple = true, Endless = true)]
    public sealed class BlastReaction : ReactionDecision
    {
        /// <summary>Seconds after a blast during which the agent still answers it.</summary>
        [Min(0f)] public float Window = 0.8f;

        public BlastReaction()
        {
            ReactionTime = 0f;
            ReactionTimeMax = 0f;
            Cooldown = 0.2f;
        }

        public override bool NeedsTarget => false;

        protected override bool Happens(AgentContext context, in TargetFacts facts, ReactionMemory memory, bool explain,
            out bool fresh)
        {
            fresh = false;
            Blackboard board = context.Blackboard;
            if (!board.TryGetKey(EliteBrain.BLAST_COUNT, out BlackboardKey<int> countKey) ||
                !board.TryGetKey(EliteBrain.BLAST_TIME, out BlackboardKey<float> timeKey))
            {
                memory.WhyNot = "no blast";
                return false;
            }

            int count = board.Get(countKey);
            fresh = count != memory.SeenHits;
            memory.SeenHits = count;
            if (count == 0)
            {
                memory.WhyNot = "no blast";
                return false;
            }

            float since = context.LocalTime - board.Get(timeKey);
            if (since > Window)
            {
                memory.WhyNot = explain ? $"blast {since:0.0} s ago, answers within {Window:0.0}" : "no blast lately";
                return false;
            }

            return true;
        }
    }
}
