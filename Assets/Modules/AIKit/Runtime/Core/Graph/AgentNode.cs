using System;

namespace AIKit
{
    /// <summary>
    /// Anything the graph is built from: a state, a task, a condition. Plain C# — a node knows nothing about Unity
    /// components, the editor or the game; it reaches the world only through AgentContext.
    /// Nodes are data: public fields, a parameterless constructor, [AIKitNode] for the registry — what a graph asset
    /// saves and loads. A node holds no per-agent state: one graph serves any number of agents, and what changes while
    /// a node runs lives in the agent's NodeMemory (Memory&lt;T&gt; of a task, Record&lt;T&gt; here). Id is the node's
    /// stable identity in its graph (edges, saves, EnterState); the builder numbers the nodes that have none.
    /// </summary>
    [Serializable]
    public abstract class AgentNode
    {
        /// <summary>
        /// The node's service name: the type title of what it does (NodeTitles.NameByType keeps it so in the builders and
        /// the graph window); a graph built in code may name its nodes to tell them apart. Empty — the type title.
        /// </summary>
        public string Name;

        /// <summary>Stable id inside the graph; 0 — not assigned yet (GraphBuilder.Build numbers them).</summary>
        public int Id;

        /// <summary>
        /// The label the user gave the node on the canvas (step 90): shown instead of the type title wherever the node is
        /// named — its title, the links to it, "not from", the decision trace, the graph's problems, the name over the
        /// character's head. Empty — the node is called by its type. Builders and code never set it.
        /// </summary>
        public string Label = "";

        /// <summary>The graph that linked the node and its cell in that graph's memory (set by BehaviorGraph.Link).</summary>
        [NonSerialized] internal BehaviorGraph Space;
        [NonSerialized] internal int Slot = -1;

        // Without a context (a node ticked by hand in a test) there is only one "agent": the node itself.
        [NonSerialized] private object _detached;

        /// <summary>
        /// What the node is called: the user's label, else its name, else the title of its type in the registry (a state —
        /// of the block it holds). Never the C# class name.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Label))
                    return Label;
                if (!string.IsNullOrEmpty(Name))
                    return Name;

                if (this is AgentState state)
                {
                    foreach (AgentTask task in state.Tasks)
                    {
                        if (!task.IsBackground)
                            return NodeTitles.Of(task);
                    }
                }

                return NodeTitles.Of(this);
            }
        }

        /// <summary>This agent's record of the node (made on first ask).</summary>
        protected internal T Record<T>(AgentContext context) where T : class, new()
        {
            if (context == null)
            {
                if (_detached is not T detached)
                {
                    detached = new T();
                    _detached = detached;
                }

                return detached;
            }

            return context.Memory.Of<T>(Space, Slot, this);
        }

        public override string ToString() => DisplayName;
    }
}
