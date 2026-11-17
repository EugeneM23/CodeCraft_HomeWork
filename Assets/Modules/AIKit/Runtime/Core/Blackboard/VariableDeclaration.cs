using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>The value types a graph asset can declare a variable of.</summary>
    public enum VariableType
    {
        Bool,
        Int,
        Float,
        String,
        Vector3,

        /// <summary>An object of the scene. A graph asset cannot hold it: each character binds its own (AICharacter).</summary>
        Transform,

        /// <summary>A number of double precision (15.11.2026). Added last: the values of the types above are kept.</summary>
        Double
    }

    /// <summary>
    /// A variable a graph declares: its name, type, scope and starting value. The graph's agents get it on their
    /// blackboard when the graph starts (a value already there is kept), and the asset's validator checks every name
    /// a node uses against this list — a typo is an error at compile time, not a silent second variable.
    /// </summary>
    [Serializable]
    public sealed class VariableDeclaration
    {
        public string Name;
        public VariableType Type = VariableType.Float;
        public BlackboardScope Scope = BlackboardScope.Agent;

        public bool Bool;
        public int Int;
        public float Float;
        public string String;
        public Vector3 Vector3;
        public double Double;

        public VariableDeclaration()
        {
        }

        public VariableDeclaration(string name, VariableType type, BlackboardScope scope = BlackboardScope.Agent)
        {
            Name = name;
            Type = type;
            Scope = scope;
        }

        /// <summary>The CLR type of the variable.</summary>
        public Type ValueType => Type switch
        {
            VariableType.Bool => typeof(bool),
            VariableType.Int => typeof(int),
            VariableType.Float => typeof(float),
            VariableType.String => typeof(string),
            VariableType.Transform => typeof(Transform),
            VariableType.Vector3 => typeof(Vector3),
            VariableType.Double => typeof(double),
            _ => typeof(float)
        };

        /// <summary>Declares the variable on a blackboard (a present value is kept).</summary>
        public void DeclareOn(Blackboard board)
        {
            if (board == null || string.IsNullOrEmpty(Name))
                return;

            switch (Type)
            {
                case VariableType.Bool: board.Declare(Name, Bool); break;
                case VariableType.Int: board.Declare(Name, Int); break;
                case VariableType.Float: board.Declare(Name, Float); break;
                case VariableType.String: board.Declare(Name, String); break;
                case VariableType.Transform: board.Declare<Transform>(Name); break;
                case VariableType.Double: board.Declare(Name, Double); break;
                default: board.Declare(Name, Vector3); break;
            }
        }
    }
}
