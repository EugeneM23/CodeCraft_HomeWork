using System;

namespace AIKit
{
    /// <summary>
    /// Marks a graph node type for the registry (the graph editor's menu, the graph asset's loader).
    /// Id is the type's stable identity in saved graphs — it outlives renames and moves of the class, so a renamed
    /// node keeps loading without [MovedFrom] (use MovedFrom only when Unity itself must find the old type name).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AIKitNodeAttribute : Attribute
    {
        public AIKitNodeAttribute(string path, string category, string id)
        {
            Path = path;
            Category = category;
            Id = id;
        }

        /// <summary>
        /// Where the node sits in the canvas's list of nodes, and what it is called: the folders are the groups of the
        /// list, the last part is the node's title ("AI/Actions/Movement/Move To", "AI/Decisions/Movement/Chase"). The list is built
        /// from this alone (handbook §3.3, §31.2: a node appears in the menu without an edit of the editor).
        /// </summary>
        public string Path { get; }

        /// <summary>Nodes without an Order of their own come after those with one in their folder (by title).</summary>
        public const int UNORDERED = 1000;

        /// <summary>
        /// Where the node stands among the nodes of its folder in the list: smaller first; equal — by title. The default
        /// (UNORDERED) puts it after the nodes that have one.
        /// </summary>
        public int Order { get; set; } = UNORDERED;

        /// <summary>State, Task, Flow, Condition or Code (lambda nodes that a graph asset cannot hold).</summary>
        public string Category { get; }

        /// <summary>Stable type id: "aikit.task.move-to". Never change it once graphs are saved.</summary>
        public string Id { get; }

        /// <summary>
        /// A task's name for "it worked" ("Reach Position"): the graph editor shows it on the output pin whose arrow is
        /// followed when the state's tasks succeed. Empty — "Success".
        /// </summary>
        public string Success { get; set; }

        /// <summary>A task's name for "it did not work" ("No Path"); empty — "Failure".</summary>
        public string Failure { get; set; }

        /// <summary>
        /// The node is offered by the graph canvas ("Add Node", "+ when"): it works on its own, with the fields the
        /// canvas can show, on a character that has only a body and a graph. The rest (perception, combat, signals,
        /// composites) stay available to graphs built in code.
        /// </summary>
        public bool Simple { get; set; }

        /// <summary>
        /// The task works until something takes the control away (a patrol): it has no outcome, so the canvas draws no
        /// outcome pins for it — only arrows with conditions and sensors lead out of such a node.
        /// </summary>
        public bool Endless { get; set; }

        /// <summary>The task always ends in success (a wait, a log line): the canvas draws no failure pin for it.</summary>
        public bool NeverFails { get; set; }

        /// <summary>
        /// A sensor that works by itself and writes a variable of its own (Time Of Day, Senses, Squad Sight) — not a shape
        /// of seeing wired into Detection (Radius, Cone, Zone…). The list of nodes adds it on its own, never into
        /// Detection (step 90).
        /// </summary>
        public bool WorksAlone { get; set; }

        /// <summary>
        /// The task has one outcome on the canvas: a single pin by this name, followed when the task ends — whatever
        /// it ended with (Use Ability: used one or found none ready, the agent goes on from here either way). The
        /// canvas draws this pin instead of the success and the failure pins; its arrows fire on TasksFinished.
        /// </summary>
        public string Finished { get; set; }

        /// <summary>Nodes of this category wrap code (a lambda) and cannot live in a graph asset.</summary>
        public const string CODE = "Code";
    }
}
