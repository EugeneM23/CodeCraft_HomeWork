using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>One output of a Random node: how often it is picked, and whether it may be picked twice in a row.</summary>
    [Serializable]
    public sealed class RandomOutput
    {
        /// <summary>How often, against the other outputs' weights; 0 — never.</summary>
        [Min(0f)] public float Weight = 1f;

        /// <summary>On — never picked right after itself (while another output can be picked).</summary>
        public bool NotTwiceInARow;
    }

    /// <summary>
    /// Picks one of its outputs at random, by their weights, and the flow goes down that one — at once, it takes no
    /// time (handbook §20b.2, §20b.4: a weighted roulette, no repeat by a lowered chance). A ready block for the pauses
    /// between the series (§20b.4 p. 6): stand, step, dash, a taunt — each a block of its own on an output, and its
    /// wire back into Random rolls again. Outputs are added on the node; an output with no wire, or with weight 0, is
    /// never picked; none can be — the flow ends here. The random is the agent's own, with its seed.
    /// </summary>
    [Serializable]
    [AIKitNode("Graph/Flow/Random", "Task", "aikit.task.random", Simple = true, NeverFails = true, Order = 20)]
    public sealed class PickRandomTask : AgentTask
    {
        private sealed class Mem
        {
            public int Last = -1;
        }

        /// <summary>The outputs, by number: the edges of the node carry their number (Transition.Output).</summary>
        public List<RandomOutput> Outputs = new() { new RandomOutput(), new RandomOutput() };

        public override bool IsInstant => true;

        protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Success;

        /// <summary>The output picked last for this agent (-1 — none yet).</summary>
        public int LastFor(AgentContext context) => Memory<Mem>(context).Last;

        /// <summary>
        /// Rolls the output the flow goes down now: one with a wire (an edge of the owner with its number) and weight
        /// above 0, not the last one if that one may not come twice in a row and another can. -1 — none can.
        /// </summary>
        public int Pick(AgentContext context, AgentState owner)
        {
            Mem m = Memory<Mem>(context);
            int last = m.Last;
            bool lastBarred = last >= 0 && last < Outputs.Count && Outputs[last].NotTwiceInARow &&
                              CountPickable(owner, last) > 0;

            float total = 0f;
            for (int i = 0; i < Outputs.Count; i++)
            {
                if (Pickable(owner, i) && !(lastBarred && i == last))
                    total += Outputs[i].Weight;
            }

            if (total <= 0f)
                return -1;

            float roll = context != null ? context.Range(0f, total) : 0f;
            int picked = -1;
            for (int i = 0; i < Outputs.Count; i++)
            {
                if (!Pickable(owner, i) || lastBarred && i == last)
                    continue;

                picked = i;
                roll -= Outputs[i].Weight;
                if (roll < 0f)
                    break;
            }

            m.Last = picked;
            return picked;
        }

        // An output that can be picked: weight above 0 and a wire out of it
        private bool Pickable(AgentState owner, int output)
        {
            if (output < 0 || output >= Outputs.Count || Outputs[output].Weight <= 0f || owner == null)
                return false;

            for (int i = 0; i < owner.Transitions.Count; i++)
            {
                Transition edge = owner.Transitions[i];
                if (edge.Output == output && edge.Target != null)
                    return true;
            }

            return false;
        }

        private int CountPickable(AgentState owner, int except)
        {
            int count = 0;
            for (int i = 0; i < Outputs.Count; i++)
            {
                if (i != except && Pickable(owner, i))
                    count++;
            }

            return count;
        }
    }
}
