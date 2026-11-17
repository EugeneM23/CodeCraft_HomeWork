using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Ticking
{
    /// <summary>
    /// Identity of an object, not its equality: UnityEngine.Object overrides Equals (two destroyed objects are "equal"
    /// to each other), and a destroyed listener still has to be found and removed.
    /// </summary>
    internal sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        public new bool Equals(object x, object y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
