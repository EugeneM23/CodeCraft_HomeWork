using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// The one list of nodes of the canvas (step 90, C1 / C9; handbook §31.2: a library with search and groups, a node
    /// gets into it by its attribute, not by an edit of the editor). Right click on an empty place, Space and a pin let go
    /// over an empty place open the same searchable window (NodeLibrary) with a list built here: the whole list, or what
    /// that pin takes. The groups are the folders of each node's [AIKitNode] path, the order inside a folder its Order,
    /// then its title. Built without a window, so tests read it.
    /// </summary>
    public static class NodeMenu
    {
        /// <summary>What picking an item does.</summary>
        public enum Act
        {
            /// <summary>A node of the canvas (the Brain, a decision, a block): GraphDocument.AddStateWithTask.</summary>
            Node,

            /// <summary>A shape of seeing: wired into Detection (GraphDocument.AddShape).</summary>
            Shape,

            /// <summary>A sensor that works alone (Time Of Day, Senses, Squad Sight): GraphDocument.AddSensor.</summary>
            Sensor,

            /// <summary>A condition: into what asks it (a picked Branch, AND / OR), or with a Branch of its own.</summary>
            Condition,

            /// <summary>A flag of the blackboard asked "is it on" (a Compare of the flag).</summary>
            Flag,

            /// <summary>A value node (Get, What…), not wired yet: GraphDocument.AddValue.</summary>
            Value
        }

        /// <summary>One item of the list.</summary>
        public readonly struct Item
        {
            public Item(string path, string id, NodeKind kind, Act act, Color tint)
            {
                Path = path;
                Id = id;
                Kind = kind;
                Act = act;
                Tint = tint;
            }

            /// <summary>Where it sits in the list: folders and its name ("AI/Decisions/Movement/Chase (decision)").</summary>
            public string Path { get; }

            /// <summary>The node type's id in the registry; a flag — FLAG + the variable's name.</summary>
            public string Id { get; }

            public NodeKind Kind { get; }

            public Act Act { get; }

            /// <summary>The colour of the tile before its name (the colour of its title on the canvas).</summary>
            public Color Tint { get; }

            /// <summary>The name shown (the last part of the path).</summary>
            public string Name => NodeTitles.LeafOf(Path);

            public override string ToString() => Path;
        }

        /// <summary>The id of a flag item: this, then the name of the flag variable.</summary>
        public const string FLAG = "flag:";

        // The order of the folders of the list (spec 4.1; two roots since 15.11.2026, the user's word: what the AI thinks
        // and does, then the operations of the canvas): the deciding layer first, then the senses, then what a chain does;
        // then the graph's own tools. Only the order of the groups — which node is in which group is the node's own path.
        // A folder not named here comes after the named ones of its parent, by name
        private static readonly string[] FolderOrder =
        {
            "AI", "AI/Brain", "AI/Decisions", "AI/Decisions/Combat", "AI/Decisions/Movement", "AI/Decisions/Objects",
            "AI/Decisions/Reactions", "AI/Decisions/Life", "AI/Decisions/Squad",
            "AI/Senses", "AI/Actions", "AI/Actions/Movement", "AI/Actions/Combat", "AI/Actions/Objects", "AI/Actions/Life",
            "AI/Actions/Squad",
            "Graph", "Graph/Flow", "Graph/Conditions", "Graph/Conditions/Flags", "Graph/Data", "Graph/Variables", "Graph/Debug"
        };

        // ---------- the lists ----------

        /// <summary>
        /// Everything the canvas can add (right click on an empty place, Space): the Brain (unless the graph has one), the
        /// decisions that fit the graph's Brain (a squad's for a Squad Brain, a character's for a Brain), the blocks, the
        /// sensors, the conditions with the flags of the blackboard, the value nodes.
        /// </summary>
        public static List<Item> Everything(GraphDocument document)
        {
            var items = new List<Item>();
            CombatBrainTask brain = BrainOf(document);
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (Offered(entry, brain, document != null && HasBrain(document), out Act act))
                    items.Add(ItemOf(entry, act));
            }

            items.AddRange(Flags(document));
            return Finish(items);
        }

        /// <summary>
        /// What a flow pin can lead to: from the Brain's Decisions pin — the decisions that fit it; from an On Tick pin or a
        /// chain that runs from one — the instant blocks; from any other pin — the blocks (no Brain, no decision).
        /// </summary>
        public static List<Item> After(GraphDocument document, bool fromBrain, bool instantOnly)
        {
            var items = new List<Item>();
            CombatBrainTask brain = BrainOf(document);
            bool hasBrain = document != null && HasBrain(document);
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (!Offered(entry, brain, hasBrain, out Act act) || act != Act.Node)
                    continue;

                NodeKind kind = NodeKinds.Of(entry.Type);
                if (fromBrain ? kind != NodeKind.Decision : kind != NodeKind.Block)
                    continue;

                if (instantOnly && !fromBrain && AIKitNodeRegistry.Create(entry.Info.Id) is not AgentTask { IsInstant: true })
                    continue;

                items.Add(ItemOf(entry, act));
            }

            return Finish(items);
        }

        /// <summary>The shapes of seeing Detection takes (its Sensors pin).</summary>
        public static List<Item> Shapes()
        {
            var items = new List<Item>();
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (Offered(entry, null, false, out Act act) && act == Act.Shape)
                    items.Add(ItemOf(entry, act));
            }

            return Finish(items);
        }

        /// <summary>The conditions (a Condition pin, Add Condition / Add Input), with the flags of the blackboard.</summary>
        public static List<Item> Conditions(GraphDocument document, bool withFlags = true)
        {
            var items = new List<Item>();
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (Offered(entry, null, false, out Act act) && act == Act.Condition)
                    items.Add(ItemOf(entry, act));
            }

            if (withFlags)
                items.AddRange(Flags(document));
            return Finish(items);
        }

        /// <summary>The value nodes that fit a pin taking values of this type (a value input pin let go on an empty place).</summary>
        public static List<Item> Values(Type taken)
        {
            var items = new List<Item>();
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (Offered(entry, null, false, out Act act) && act == Act.Value &&
                    (taken == null || taken.IsAssignableFrom(entry.Type)))
                    items.Add(ItemOf(entry, act));
            }

            return Finish(items);
        }

        // ---------- what picking does ----------

        /// <summary>
        /// Adds what the item is, at a place of the canvas: a node, a shape into Detection, a sensor of its own, a value
        /// node, a condition into its asker (null — a new Branch asks it). Returns what was added (null — nothing).
        /// </summary>
        public static object Add(GraphDocument document, Item item, Vector2 at, object asker = null)
        {
            if (document == null)
                return null;

            switch (item.Act)
            {
                case Act.Node:
                    return document.AddStateWithTask(null, item.Id, at);
                case Act.Shape:
                    return document.AddShape(item.Id, at);
                case Act.Sensor:
                    return document.AddSensor(item.Id, at);
                case Act.Value:
                    return document.AddValue(item.Id, at);
            }

            // A condition answers to somebody: with nobody picked, a Branch of its own asks it
            if (asker == null)
            {
                AgentState branch = document.AddStateWithTask(null, BRANCH, at);
                asker = branch?.Tasks.Find(task => task is BranchTask);
                at += new Vector2(-240f, 70f);
            }

            if (asker == null)
                return null;

            if (item.Act == Act.Condition)
                return document.AddConditionTo(asker, item.Id, at);

            // A flag: a Compare that asks whether the flag is on
            if (document.AddConditionTo(asker, COMPARE, at) is not CompareCondition flag)
                return null;

            flag.Variable = item.Id.Substring(FLAG.Length);
            flag.Kind = VariableType.Bool;
            document.Touch();
            return flag;
        }

        private const string BRANCH = "aikit.task.branch";
        private const string COMPARE = "aikit.condition.compare";

        // ---------- inside ----------

        // The graph's Brain task (null — none yet)
        private static CombatBrainTask BrainOf(GraphDocument document)
        {
            if (document == null)
                return null;

            foreach (AgentState state in document.States())
            {
                foreach (AgentTask task in state.Tasks)
                {
                    if (task is CombatBrainTask brain)
                        return brain;
                }
            }

            return null;
        }

        private static bool HasBrain(GraphDocument document) => BrainOf(document) != null;

        private static readonly Dictionary<System.Reflection.Assembly, bool> TestAssemblies = new();

        // A node of a test assembly (one that references NUnit) is the tests' own: never offered on the canvas
        private static bool IsTestNode(Type type)
        {
            System.Reflection.Assembly assembly = type.Assembly;
            if (TestAssemblies.TryGetValue(assembly, out bool test))
                return test;

            test = false;
            foreach (System.Reflection.AssemblyName referenced in assembly.GetReferencedAssemblies())
                test |= referenced.Name == "nunit.framework";
            TestAssemblies[assembly] = test;
            return test;
        }

        // Whether the list offers the node at all, and what picking it does
        private static bool Offered(AIKitNodeRegistry.Entry entry, CombatBrainTask brain, bool hasBrain, out Act act)
        {
            act = Act.Node;
            if (entry.Info.Category == AIKitNodeAttribute.CODE || entry.Info.Category == "State" || IsTestNode(entry.Type))
                return false;

            switch (NodeKinds.Of(entry.Type))
            {
                case NodeKind.Brain:
                    return entry.Info.Simple && !hasBrain;     // one Brain per graph

                case NodeKind.Decision:
                    // A squad's decisions for a Squad Brain, a character's for a Brain (GraphValidator: WrongBrain)
                    return entry.Info.Simple && (brain == null ||
                                                 typeof(SquadDecisionTask).IsAssignableFrom(entry.Type) ==
                                                 brain is SquadBrainTask);

                case NodeKind.Sensor:
                    if (entry.Type == typeof(DetectionTask))
                        return false;                           // made with its first shape
                    act = entry.Info.WorksAlone ? Act.Sensor : Act.Shape;
                    return true;

                case NodeKind.Condition:
                    act = Act.Condition;
                    return entry.Info.Simple;

                case NodeKind.Value:
                    act = Act.Value;
                    return !typeof(IBlockOutput).IsAssignableFrom(entry.Type);   // a block's output comes with its block

                default:
                    // A block that leads a squad's formation belongs in a command graph only
                    return entry.Info.Category == "Task" && entry.Info.Simple &&
                           (brain == null || brain is SquadBrainTask || !typeof(SquadBlockTask).IsAssignableFrom(entry.Type));
            }
        }

        private static Item ItemOf(AIKitNodeRegistry.Entry entry, Act act) =>
            new(entry.Info.Path, entry.Info.Id, NodeKinds.Of(entry.Type), act, NodeKinds.Tint(entry.Type));

        // The flags of the blackboard, each a condition of its own: "is it on"
        private static IEnumerable<Item> Flags(GraphDocument document)
        {
            if (document == null)
                yield break;

            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (VariableDeclaration declared in document.Graph.Variables)
            {
                if (declared.Type == VariableType.Bool && !string.IsNullOrEmpty(declared.Name))
                    names.Add(declared.Name);
            }

            foreach (VariableUse use in BlackboardUsage.Collect(document))
            {
                if (use.Type == VariableType.Bool && !string.IsNullOrEmpty(use.Name))
                    names.Add(use.Name);
            }

            foreach (string name in names)
                yield return new Item($"Graph/Conditions/Flags/{name.Replace('/', ' ')}", FLAG + name, NodeKind.Condition,
                    Act.Flag, NodeKinds.FlagTint);
        }

        // Sorted the way the list shows it, a name another kind shares marked with the kind ("Chase (decision)")
        private static List<Item> Finish(List<Item> items)
        {
            var kindsOf = new Dictionary<string, HashSet<NodeKind>>();
            foreach (Item item in items)
            {
                if (!kindsOf.TryGetValue(item.Name, out HashSet<NodeKind> kinds))
                    kindsOf[item.Name] = kinds = new HashSet<NodeKind>();
                kinds.Add(item.Kind);
            }

            var marked = new List<(Item Item, int Order)>();
            foreach (Item item in items)
            {
                string word = NodeKinds.Word(item.Kind);
                Item shown = item;
                if (word != null && kindsOf[item.Name].Count > 1)
                    shown = new Item($"{item.Path} ({word})", item.Id, item.Kind, item.Act, item.Tint);

                marked.Add((shown, OrderOf(item.Id)));
            }

            marked.Sort((left, right) => Compare(left.Item.Path, left.Order, right.Item.Path, right.Order));
            return marked.ConvertAll(pair => pair.Item);
        }

        private static int OrderOf(string id)
        {
            Type type = AIKitNodeRegistry.Find(id);
            var info = type != null
                ? (AIKitNodeAttribute)Attribute.GetCustomAttribute(type, typeof(AIKitNodeAttribute))
                : null;
            return info?.Order ?? AIKitNodeAttribute.UNORDERED;
        }

        // Folder by folder: the named order of the folders, then their names; inside a folder the nodes first (by their
        // Order, then their names), then its folders
        private static int Compare(string left, int leftOrder, string right, int rightOrder)
        {
            string[] a = left.Split('/');
            string[] b = right.Split('/');
            for (int depth = 0; depth < Math.Min(a.Length, b.Length); depth++)
            {
                bool aLeaf = depth == a.Length - 1;
                bool bLeaf = depth == b.Length - 1;
                if (aLeaf && bLeaf)
                {
                    int byOrder = leftOrder.CompareTo(rightOrder);
                    return byOrder != 0 ? byOrder : string.CompareOrdinal(a[depth], b[depth]);
                }

                if (aLeaf != bLeaf)
                    return aLeaf ? -1 : 1;              // a node before the folders beside it

                if (a[depth] == b[depth])
                    continue;

                int byFolder = RankOf(string.Join("/", a, 0, depth + 1)).CompareTo(RankOf(string.Join("/", b, 0, depth + 1)));
                return byFolder != 0 ? byFolder : string.CompareOrdinal(a[depth], b[depth]);
            }

            return a.Length.CompareTo(b.Length);
        }

        private static int RankOf(string folder)
        {
            int at = Array.IndexOf(FolderOrder, folder);
            return at >= 0 ? at : FolderOrder.Length;
        }
    }
}
