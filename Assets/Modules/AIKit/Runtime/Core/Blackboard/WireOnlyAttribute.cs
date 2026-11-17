using System;

namespace AIKit
{
    /// <summary>
    /// A "number or variable" field that takes its value only by a wire from a variable of the blackboard: the canvas
    /// draws no number in its pin, and with no wire the node does nothing (the graph check names the loose pin). The
    /// rule of the canvas — a wire cut, the value gone — for a value a node must not have a silent default of.
    /// Optional — the node works without the wire too, in another way it says on itself (Use Ability's Timer: with no
    /// wire the ability's own pause is the pause): no graph check for the loose pin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class WireOnlyAttribute : Attribute
    {
        /// <summary>The node works without the wire too (step 90): a loose pin is no problem of the graph.</summary>
        public bool Optional { get; set; }
    }
}
