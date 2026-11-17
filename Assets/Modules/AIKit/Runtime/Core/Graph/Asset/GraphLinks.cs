using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// The arrows of a graph asset drawn as links (step 90, C2): an arrow that would run back across the canvas ends at
    /// a compact label "→ node" docked at its output pin instead. Layout only — the arrow is the same arrow, nothing at
    /// runtime reads it. Here: the record the asset keeps the n-th arrow of a node in, making an arrow a link, and the
    /// builders' rule (LinkLoops): every arrow whose target stands left of its source, or level with it, is a link.
    /// </summary>
    public static class GraphLinks
    {
        /// <summary>
        /// The record of the n-th arrow of a node (an arrow has no id of its own: it is the n-th id of the node's
        /// "Transitions" link); null — the node or the arrow is not in the asset.
        /// </summary>
        public static GraphRecord RecordOf(BehaviorGraphAsset asset, AgentState state, int arrow)
        {
            if (asset == null || state == null || arrow < 0)
                return null;

            GraphLink edges = asset.Find(state.Id)?.Links.Find(link => link.Field == nameof(AgentState.Transitions));
            return edges != null && arrow < edges.Ids.Count ? asset.Find(edges.Ids[arrow]) : null;
        }

        /// <summary>
        /// The n-th arrow of a node is drawn as a link docked at its output pin (its bend points go: they led to the far
        /// node). False — no such arrow in the asset.
        /// </summary>
        public static bool Link(BehaviorGraphAsset asset, AgentState state, int arrow)
        {
            GraphRecord record = RecordOf(asset, state, arrow);
            if (record == null)
                return false;

            Dock(record);
            return true;
        }

        /// <summary>
        /// Every arrow of a written graph whose target stands left of its source or level with it (by the positions in the
        /// asset) becomes a link docked at its output pin — a wire back across the canvas is not drawn. The Brain's wires
        /// stay wires (they leave the Brain's frame each by a point of its own). Call it after the graph is written and
        /// placed. Returns how many arrows became links.
        /// </summary>
        public static int LinkLoops(BehaviorGraphAsset asset, BehaviorGraph graph)
        {
            if (asset == null || graph?.Root == null)
                return 0;

            var states = new List<AgentState>();
            graph.CollectStates(states);
            int made = 0;
            foreach (AgentState state in states)
            {
                if (state == graph.Root || state.IsBrain)
                    continue;

                GraphRecord source = asset.Find(state.Id);
                if (source == null)
                    continue;

                for (int i = 0; i < state.Transitions.Count; i++)
                {
                    GraphRecord target = state.Transitions[i].Target != null ? asset.Find(state.Transitions[i].Target.Id) : null;
                    if (target == null || !RunsBack(source.Position, target.Position))
                        continue;

                    GraphRecord arrow = RecordOf(asset, state, i);
                    if (arrow == null || arrow.IsLink && !arrow.LinkFree)
                        continue;

                    Dock(arrow);
                    made++;
                }
            }

            return made;
        }

        /// <summary>
        /// An arrow from a node standing here to a node standing there runs back across the canvas: its target stands left
        /// of its source or level with it — drawn as a link at its pin, not as a long wire (the rule of LinkLoops, and of
        /// an arrow wired by hand on the canvas).
        /// </summary>
        public static bool RunsBack(UnityEngine.Vector2 source, UnityEngine.Vector2 target) => target.x <= source.x;

        private static void Dock(GraphRecord record)
        {
            record.IsLink = true;
            record.LinkFree = false;
            record.Waypoints ??= new List<UnityEngine.Vector2>();
            record.Waypoints.Clear();
        }
    }
}
