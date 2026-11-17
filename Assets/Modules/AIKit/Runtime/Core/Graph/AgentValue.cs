using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// What a value node gives: a number, a flag, an object, a filter of objects (the What of a search), or an ability
    /// (an Ability node wired into the Abilities pin of Use Any Ability — step 90).
    /// </summary>
    public enum ValueKind
    {
        Number,
        Flag,
        Object,
        Filter,
        Ability
    }

    /// <summary>
    /// A node of data on the canvas (step 90): no flow pins, one output pin of its kind, wired into a pin of a block, a
    /// condition or a decision — the getter of a visual script (Get: the health of the target; What: the kind of object a
    /// search looks for). The reader holds it in a [NonSerialized] public link field, so the asset keeps the wire and the
    /// graph visits the node like a Branch's condition. A value node only reads (invariant 6: a question makes no state),
    /// and "no data" is an answer of its own — the Try methods return false — never a zero passed off as a value
    /// (handbook §4.3, §38 №19). A subclass answers the kinds it gives and leaves the rest false.
    /// </summary>
    [Serializable]
    public abstract class AgentValue : AgentNode
    {
        /// <summary>The kind of its output pin.</summary>
        public abstract ValueKind Kind { get; }

        /// <summary>The number now; false — no data (the object does not say, nothing is wired in).</summary>
        public virtual bool TryGetNumber(AgentContext context, out float value)
        {
            value = 0f;
            return false;
        }

        /// <summary>The flag now; false — no data.</summary>
        public virtual bool TryGetFlag(AgentContext context, out bool value)
        {
            value = false;
            return false;
        }

        /// <summary>The object now; false — no data.</summary>
        public virtual bool TryGetObject(AgentContext context, out Transform value)
        {
            value = null;
            return false;
        }
    }
}
