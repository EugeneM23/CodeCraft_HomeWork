using System;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The stable id of a run skill (turret, air strike, ricochet): the key of its config in the catalog and the only thing the save file keeps (GDD §14).
    /// A string under the hood, so ids read well in the save and in the inspector and a new one needs no code.
    /// Compare with ==; default is "none".
    /// </summary>
    [Serializable]
    public struct SkillId : IEquatable<SkillId>
    {
        [SerializeField] private string _value;

        public SkillId(string value) => _value = value ?? string.Empty;

        public string Value => _value ?? string.Empty;

        public bool IsNone => string.IsNullOrEmpty(_value);

        public bool Equals(SkillId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SkillId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;

        public static bool operator ==(SkillId a, SkillId b) => a.Equals(b);

        public static bool operator !=(SkillId a, SkillId b) => !a.Equals(b);

        public static explicit operator SkillId(string value) => new(value);
    }
}
