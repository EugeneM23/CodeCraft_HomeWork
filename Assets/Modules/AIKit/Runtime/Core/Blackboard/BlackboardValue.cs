using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A node field that is either a plain number typed by a designer or a blackboard variable: one switch in the
    /// inspector. Nodes always read through Get(context), so the same node works with constants and with variables.
    /// A variable is read by key after the first look (BlackboardVariable): keep the value in a field, not a property —
    /// a property getter hands out a copy and the cached key is lost.
    /// </summary>
    [Serializable]
    public struct BlackboardValue<T> : IEquatable<BlackboardValue<T>>
    {
        [Tooltip("On — the value comes from a blackboard variable")]
        [SerializeField] private bool _useVariable;
        [SerializeField] private BlackboardScope _scope;
        [SerializeField] private string _name;
        [SerializeField] private T _constant;

        [NonSerialized] private BlackboardVariable<T> _variable;

        public bool UsesVariable => _useVariable;

        public BlackboardScope Scope => _scope;

        public string Name => _name;

        public T Get(AgentContext context) =>
            _useVariable && context != null ? _variable.Get(context.BlackboardOf(_scope), _name, _constant) : _constant;

        /// <summary>Writes back when the field points at a variable; a constant field ignores the write.</summary>
        public void Set(AgentContext context, T value)
        {
            if (_useVariable && context != null)
                _variable.Set(context.BlackboardOf(_scope), _name, value);
        }

        public static BlackboardValue<T> Constant(T value) => new() { _constant = value };

        public static BlackboardValue<T> Variable(string name, BlackboardScope scope = BlackboardScope.Agent,
            T fallback = default) =>
            new() { _useVariable = true, _name = name, _scope = scope, _constant = fallback };

        public static implicit operator BlackboardValue<T>(T value) => Constant(value);

        /// <summary>Equal when they read the same thing: the switch, the scope, the name and the constant (the cached key
        /// is not part of the value).</summary>
        public bool Equals(BlackboardValue<T> other) =>
            _useVariable == other._useVariable && _scope == other._scope && _name == other._name &&
            System.Collections.Generic.EqualityComparer<T>.Default.Equals(_constant, other._constant);

        public override bool Equals(object obj) => obj is BlackboardValue<T> other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(_useVariable, _scope, _name, _constant);

        public override string ToString() => _useVariable ? $"${_name}" : _constant?.ToString() ?? "null";
    }
}
