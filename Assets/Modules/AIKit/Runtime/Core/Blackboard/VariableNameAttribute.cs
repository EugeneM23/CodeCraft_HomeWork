using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Marks a string field of a node that names a blackboard variable: the graph asset's validator checks the name
    /// against the asset's declarations (a typo is an error, not a silent second variable), and the graph editor offers
    /// the declared names.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class VariableNameAttribute : PropertyAttribute
    {
        public VariableNameAttribute()
        {
        }

        /// <summary>Only variables of this type fit the field (the editor offers those).</summary>
        public VariableNameAttribute(VariableType type)
        {
            Type = type;
            HasType = true;
        }

        public VariableType Type { get; }

        public bool HasType { get; }
    }
}
