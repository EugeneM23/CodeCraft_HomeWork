using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// A behavior — the canvas as the program: sensors (the root's background tasks) write variables, the Brain chooses
    /// a decision, the decision's chain of blocks runs, and when the chain ends the Brain chooses again. The graph is flat:
    /// the root holds the sensors and every node (the Brain, the decisions, the blocks); arrows are the Brain's wires, the
    /// decisions' Do, the blocks' outputs and their On Tick pins. Nothing else runs: a graph without a Brain does nothing,
    /// a block no chain reaches never starts. Built with BrainGraphBuilder in code or compiled from a BehaviorGraphAsset.
    /// A description only — any number of agents run one graph, each through its own BehaviorGraphInstance, with
    /// everything that changes in the agent's NodeMemory. Linking gives every node a memory slot (a think never hashes)
    /// and finds the Brain and the decision each block belongs to.
    /// </summary>
    public sealed class BehaviorGraph
    {
        private readonly Dictionary<AgentState, AgentState> _chainOf = new();

        public BehaviorGraph(AgentState root)
        {
            Root = root;
            Link();
        }

        /// <summary>The invisible root: the sensors are its tasks, the nodes its children.</summary>
        public AgentState Root { get; }

        /// <summary>The Brain the graph starts in (the first node with a Brain task); null — the graph does nothing.</summary>
        public AgentState Brain { get; private set; }

        /// <summary>Memory cells an agent needs for this graph (one per node).</summary>
        public int SlotCount { get; private set; }

        /// <summary>The variables the graph declares (an asset's list); an agent's blackboard gets them on start.</summary>
        public readonly List<VariableDeclaration> Variables = new();

        /// <summary>Re-links the nodes (the root as their parent), finds the Brain and the chains: call it after changing the graph by hand.</summary>
        public void Link()
        {
            if (Root != null)
            {
                Root.Parent = null;
                for (int i = 0; i < Root.Children.Count; i++)
                {
                    if (Root.Children[i] != null)
                        Root.Children[i].Parent = Root;
                }
            }

            Brain = null;
            for (int i = 0; Root != null && i < Root.Children.Count && Brain == null; i++)
            {
                if (Root.Children[i] != null && Root.Children[i].IsBrain)
                    Brain = Root.Children[i];
            }

            FindChains();
            AssignSlots();
        }

        public BehaviorGraphInstance CreateInstance() => new(this);

        /// <summary>
        /// The decision whose chain the node belongs to (the first one whose Do or On Tick leads to it); the decision itself
        /// for a decision; null — the Brain, or a node no chain reaches (it never runs).
        /// </summary>
        public AgentState ChainOwnerOf(AgentState node) =>
            node != null && _chainOf.TryGetValue(node, out AgentState decision) ? decision : null;

        /// <summary>The node runs at some point: the Brain, a decision wired to it, or a block a decision's chain reaches.</summary>
        public bool Runs(AgentState node) => node != null && (node == Brain || _chainOf.ContainsKey(node));

        // From each decision wired to the Brain: every node its Do, its blocks' outputs and their On Tick pins lead to. An
        // On Tick chain runs only blocks that take no time (BehaviorGraphInstance.RunTickPins): a block there that is not
        // instant — and whatever only it leads to — never runs, unless a chain of the flow reaches it too
        private void FindChains()
        {
            _chainOf.Clear();
            if (Brain == null)
                return;

            var queue = new Queue<AgentState>();
            var onTick = new Queue<bool>();
            var tickOnly = new HashSet<AgentState>();       // reached so far only within an On Tick chain
            for (int i = 0; i < Brain.Transitions.Count; i++)
            {
                AgentState decision = Brain.Transitions[i].Target;
                if (decision == null || decision.Reasoner == null || _chainOf.ContainsKey(decision))
                    continue;

                _chainOf[decision] = decision;
                queue.Enqueue(decision);
                onTick.Enqueue(false);
                while (queue.Count > 0)
                {
                    AgentState node = queue.Dequeue();
                    bool inTick = onTick.Dequeue();
                    for (int t = 0; t < node.Transitions.Count; t++)
                    {
                        AgentState next = node.Transitions[t].Target;
                        if (next == null || next == Brain || next.Reasoner != null)
                            continue;

                        bool nextInTick = inTick || node.Transitions[t].Trigger == TransitionTrigger.Tick;
                        if (nextInTick && !next.IsInstant)
                            continue;                   // On Tick runs no block that takes time: the wire does nothing

                        if (_chainOf.ContainsKey(next))
                        {
                            // Reached before only within an On Tick chain, now by the flow: what it leads to runs too
                            if (!nextInTick && tickOnly.Remove(next))
                            {
                                queue.Enqueue(next);
                                onTick.Enqueue(false);
                            }

                            continue;
                        }

                        _chainOf[next] = decision;
                        if (nextInTick)
                            tickOnly.Add(next);
                        queue.Enqueue(next);
                        onTick.Enqueue(nextInTick);
                    }
                }
            }
        }

        private void AssignSlots()
        {
            int next = 0;
            var seen = new HashSet<object>();
            VisitNodes(node =>
            {
                if (seen.Add(node))
                {
                    node.Space = this;
                    node.Slot = next++;
                }
            });

            SlotCount = next;
        }

        /// <summary>
        /// Numbers every node that has no id yet — nodes, their tasks with what they hold — in a fixed walk, so the same
        /// build gives the same ids; ids already set are kept and never reused. Arrows get their TargetId from the target.
        /// The builder calls it; a graph assembled by hand calls it after Link.
        /// </summary>
        public void AssignIds() => AssignIds(null);

        /// <summary>
        /// The same, with ids that are taken elsewhere: a node numbered here never gets one of them (an upgrade step
        /// numbers the nodes it adds away from every record of the asset, those not read yet included — an old record's
        /// look, kept by its id, must not land on a new node).
        /// </summary>
        public void AssignIds(ICollection<int> reserved)
        {
            int next = 1;
            VisitNodes(node => next = Math.Max(next, node.Id + 1));
            VisitNodes(node =>
            {
                if (node.Id != 0)
                    return;

                while (reserved != null && reserved.Contains(next))
                    next++;
                node.Id = next++;
            });

            var states = new List<AgentState>();
            CollectStates(states);
            for (int s = 0; s < states.Count; s++)
            {
                for (int t = 0; t < states[s].Transitions.Count; t++)
                    states[s].Transitions[t].TargetId = states[s].Transitions[t].Target?.Id ?? 0;
            }
        }

        /// <summary>Every node of the graph once: the root, the nodes, their tasks and what those hold (the shapes of
        /// Detection, the condition of a Branch). The validator and the id pass use it.</summary>
        public void VisitNodes(Action<AgentNode> visit)
        {
            var states = new List<AgentState>();
            CollectStates(states);
            for (int s = 0; s < states.Count; s++)
            {
                AgentState state = states[s];
                visit(state);
                for (int i = 0; i < state.Tasks.Count; i++)
                    WalkNode(state.Tasks[i], visit);
                for (int i = 0; i < state.Values.Count; i++)
                    WalkNode(state.Values[i], visit);       // the root's value nodes nobody reads yet (step 90)
            }
        }

        // A task and every node it holds in its link fields (the shapes of Detection, a Branch's condition and the
        // conditions of AND / OR) — nodes of the graph are not entered from here: the root's list owns them
        private static void WalkNode(AgentNode node, Action<AgentNode> visit)
        {
            if (node == null)
                return;

            visit(node);
            GraphFields.Link[] links = GraphFields.LinksOf(node.GetType());
            for (int i = 0; i < links.Length; i++)
            {
                GraphFields.Link link = links[i];
                if (link.IsObject || link.Holds(typeof(AgentState)) || link.Holds(typeof(Transition)))
                    continue;

                object value = link.Field.GetValue(node);
                if (!link.IsList)
                {
                    WalkNode(value as AgentNode, visit);
                    continue;
                }

                if (value is System.Collections.IList list)
                {
                    for (int j = 0; j < list.Count; j++)
                        WalkNode(list[j] as AgentNode, visit);
                }
            }
        }

        /// <summary>All nodes of the graph, the root first (debug, validation, editor).</summary>
        public void CollectStates(List<AgentState> result)
        {
            result.Clear();
            if (Root == null)
                return;

            result.Add(Root);
            for (int i = 0; i < Root.Children.Count; i++)
            {
                if (Root.Children[i] != null)
                    result.Add(Root.Children[i]);
            }
        }
    }
}
