using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>One element of a graph asset: a node or an edge, its data and its links.</summary>
    [Serializable]
    public sealed class GraphRecord
    {
        /// <summary>The element's id in the asset (a node's Id; edges get theirs from the writer).</summary>
        public int Id;

        /// <summary>The stable type id from [AIKitNode] (an edge — GraphSerializer.TRANSITION): survives a class rename.</summary>
        public string Type;

        /// <summary>The element's own fields as JSON (JsonUtility; [FormerlySerializedAs] keeps a renamed field).</summary>
        [TextArea(1, 6)] public string Data;

        public List<GraphLink> Links = new();

        /// <summary>Where the editor draws it (states only).</summary>
        public Vector2 Position;

        /// <summary>
        /// The points the editor bends the wire of the element through, in order: the arrow of an edge, or the data
        /// wire that comes into a task. Layout only — nothing at runtime reads it.
        /// </summary>
        public List<Vector2> Waypoints = new();

        /// <summary>
        /// An edge only: the editor does not draw its arrow all the way to the node it leads to — the arrow ends at a
        /// small label with that node's name (a long wire across the canvas replaced with a short one). The label stands
        /// at its output pin (docked, stacked under the other labels of that pin), unless LinkFree. Layout only —
        /// nothing at runtime reads it.
        /// </summary>
        public bool IsLink;

        /// <summary>
        /// A link label put where the user dragged it (at LinkPosition), not docked at its output pin (step 90: a link
        /// stands at its pin by default; an older graph's links dock when it is opened). Layout only.
        /// </summary>
        public bool LinkFree;

        /// <summary>Where a free link label stands (LinkFree); a docked one ignores it.</summary>
        public Vector2 LinkPosition;

        /// <summary>
        /// A wire of the Brain only: the side of the Brain's frame the user wired it from (step 90, C10) — kept, so the
        /// wire leaves where it was put. None — the wire leaves the side that faces its decision, spread with the others.
        /// Layout only.
        /// </summary>
        public FrameSide ExitSide;

        /// <summary>How far along ExitSide the wire leaves the frame: 0 — the side's start (top or left), 1 — its end.</summary>
        public float ExitAlong;

        /// <summary>
        /// A state only: the editor folds its output pins that lead nowhere into the card (a new node starts folded; a
        /// click on the card's fold row opens them). Layout only — nothing at runtime reads it.
        /// </summary>
        public bool Folded;
    }

    /// <summary>A side of a box on the canvas (the Brain's frame a wire leaves). None — not set.</summary>
    public enum FrameSide
    {
        None,
        Left,
        Right,
        Top,
        Bottom
    }

    /// <summary>One variable node of the canvas: which variable, and where it is drawn.</summary>
    [Serializable]
    public sealed class GraphVariableNode
    {
        public string Variable;
        public Vector2 Position;

        /// <summary>The ids of the tasks whose object pin is wired from this very node (a wire stays where it was put).</summary>
        public List<int> Readers = new();

        /// <summary>The arrow of the sensor that fills the variable comes into this node (one per variable).</summary>
        public bool Filled;
    }

    /// <summary>
    /// A frame of the canvas: a titled box drawn around some nodes (a comment: "melee", "waiting for a place").
    /// The nodes inside are named by id (states and sensors). Layout only — nothing at runtime reads it.
    /// </summary>
    [Serializable]
    public sealed class GraphFrame
    {
        public string Title = "Comment";
        public List<int> Members = new();

        /// <summary>
        /// The id of the reasoner state (Combat Brain) this frame is the picture of; 0 — a plain frame, a comment.
        /// A brain frame shows the brain's rules in its header, and the nodes inside are the brain's actions.
        /// </summary>
        public int Brain;
    }

    /// <summary>
    /// A part of the graph collapsed into one node (15.11.2026, the user's word; Unreal's Collapse Nodes): on the canvas its
    /// nodes stand behind one node with the pins of the wires that cross its edge, and a double click opens a canvas of
    /// their own. Only a look of the editor: the graph that runs is the same flat graph (nothing at runtime reads it).
    /// </summary>
    [Serializable]
    public sealed class GraphSubgraph
    {
        /// <summary>Its own number (the canvas finds it by it; never a node's id).</summary>
        public int Id;

        public string Title = "Subgraph";

        /// <summary>The ids of the nodes inside (blocks and decisions — never the Brain).</summary>
        public List<int> Members = new();

        /// <summary>Where its node stands on the canvas that holds it.</summary>
        public Vector2 Position;
    }

    /// <summary>A link field of a record: the ids it holds (in order), or the index of a Unity asset.</summary>
    [Serializable]
    public sealed class GraphLink
    {
        public string Field;
        public List<int> Ids = new();
        public int Object = -1;
    }

    /// <summary>
    /// A behavior graph as an asset: records of nodes and arrows with their links by id (flat — no nested references),
    /// the root and the variables the graph declares. Compile() turns it into a BehaviorGraph once and
    /// keeps it: one compiled description for every agent that runs the asset, each with its own instance and memory.
    /// Compiling validates; problems are logged with the asset as the context (a click selects it) and named by node.
    /// Edited with the graph window (Window → AI Kit → Behavior Graph) or written from a graph built in code
    /// (GraphSerializer.Write).
    /// </summary>
    [CreateAssetMenu(menuName = "AI Kit/Behavior Graph", fileName = "BehaviorGraph")]
    public sealed class BehaviorGraphAsset : ScriptableObject
    {
        public int RootId;

        /// <summary>
        /// The format the records were written in (handbook §29а.3, C.5): GraphUpgrades brings an older graph to the
        /// current picture when it is read; a write stamps the current version. 0 — written before step 90.
        /// </summary>
        public int FormatVersion;

        /// <summary>The last read of this asset upgraded an older format (the editor saves the upgraded graph).</summary>
        [NonSerialized] public bool UpgradedOnRead;

        /// <summary>
        /// Old assets only: the reflex arrows of the old model (a sensor's Detected / Lost, From anywhere). Never written
        /// now; an asset that has any is read for viewing only and shows the problem (GraphIssueCode.OldModel).
        /// </summary>
        [HideInInspector] public List<int> Reflexes = new();

        public List<GraphRecord> Records = new();
        public List<VariableDeclaration> Variables = new();

        /// <summary>
        /// The variable nodes of the canvas: a variable drawn as a small node — the node that fills it points an arrow
        /// at it, the nodes that read it get their arrow from it. One variable may have several, placed next to the
        /// nodes that use it, so no wire runs across the whole graph. Layout only — nothing at runtime reads it.
        /// </summary>
        public List<GraphVariableNode> VariableNodes = new();

        /// <summary>The frames of the canvas (titled boxes around groups of nodes). Layout only.</summary>
        public List<GraphFrame> Frames = new();

        /// <summary>The parts of the graph collapsed into one node each (a look of the editor only).</summary>
        public List<GraphSubgraph> Subgraphs = new();

        /// <summary>The Unity assets nodes point to (a subgraph's asset), by index from a link.</summary>
        public List<UnityEngine.Object> Objects = new();

        [NonSerialized] private BehaviorGraph _compiled;
        [NonSerialized] private List<GraphIssue> _issues;

        // The asset was compiled and could not be read: not read (and logged) again until it changes
        [NonSerialized] private bool _compileFailed;

        /// <summary>The problems the last compile found (empty — clean).</summary>
        public IReadOnlyList<GraphIssue> Issues => _issues ??= GraphValidator.Validate(this);

        /// <summary>
        /// The compiled graph (made once; null — the asset cannot be compiled, see Issues). An asset that cannot be read is
        /// read and its problems logged once: every character with it asks, and asks again, until the asset changes.
        /// </summary>
        public BehaviorGraph Compile()
        {
            if (_compiled != null || _compileFailed)
                return _compiled;

            _issues = new List<GraphIssue>();
            _compiled = GraphSerializer.Read(this, _issues);
            _compileFailed = _compiled == null;
            if (_compiled != null)
                GraphValidator.Check(this, _compiled, _issues);

            foreach (GraphIssue issue in _issues)
            {
                if (issue.IsError)
                    Debug.LogError($"{name}: {issue}", this);
                else
                    Debug.LogWarning($"{name}: {issue}", this);
            }

            return _compiled;
        }

        /// <summary>The asset changed (the editor, a write): the next Compile reads it anew.</summary>
        public void MarkChanged()
        {
            _compiled = null;
            _issues = null;
            _peeked = null;
            _compileFailed = false;
            _peekFailed = false;
        }

        [NonSerialized] private BehaviorGraph _peeked;
        [NonSerialized] private bool _peekFailed;

        /// <summary>
        /// The graph for looking at, not for running (a gizmo that draws the graph's sensors): read once per change and
        /// kept — a read that found no root too (a gizmo asks on every repaint) — and nothing is logged: a graph that is
        /// still being drawn has issues all the time. Null — no root.
        /// </summary>
        public BehaviorGraph Peek()
        {
            if (_compiled != null)
                return _compiled;

            if (_peeked != null || _peekFailed)
                return _peeked;

            _peeked = GraphSerializer.Read(this, new List<GraphIssue>());
            _peekFailed = _peeked == null;
            return _peeked;
        }

        public GraphRecord Find(int id)
        {
            for (int i = 0; i < Records.Count; i++)
            {
                if (Records[i].Id == id)
                    return Records[i];
            }

            return null;
        }

        private void OnValidate() => MarkChanged();
    }
}
