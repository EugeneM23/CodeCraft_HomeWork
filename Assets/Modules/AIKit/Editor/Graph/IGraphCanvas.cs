using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace AIKit.Editor
{
    /// <summary>
    /// What the graph window needs from a canvas (handbook §31: the editor framework stays behind this line, so moving
    /// from GraphView to Graph Toolkit replaces one class). The canvas shows a GraphDocument, reports what the user
    /// picked and asks the document for every edit; it never reads or writes the asset itself.
    /// </summary>
    public interface IGraphCanvas
    {
        VisualElement Element { get; }

        /// <summary>Redraws the whole document (after an edit, an undo, a reload).</summary>
        void Show(GraphDocument document);

        /// <summary>The element the user selected (a state, an edge; null — nothing).</summary>
        event Action<object> Selected;

        /// <summary>
        /// Runtime debugging, called every editor update: the running agent's path (state ids), its leaf with the time
        /// in it, and the edge it took last (stamp — the time of that decision: a change means "taken again").
        /// </summary>
        void Highlight(IReadOnlyCollection<int> activeStateIds, int leafId, float timeInLeaf, Transition last,
            float stamp);

        /// <summary>The states selected on the canvas (extract to subgraph).</summary>
        IReadOnlyList<AgentState> SelectedStates { get; }

        /// <summary>Runtime debugging: the sensors that see something now light up (null — none).</summary>
        void HighlightSensors(Blackboard board);

        void Frame();

        /// <summary>Selects the node of a record (a state, a sensor, a condition) and brings it into view.</summary>
        void FocusNode(int id);
    }
}
