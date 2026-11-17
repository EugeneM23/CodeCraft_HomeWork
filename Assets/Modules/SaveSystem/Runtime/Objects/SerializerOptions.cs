using System;
using System.Collections.Generic;

namespace Saving.Objects
{
    /// <summary>The options the Saveable turned on for the component being saved or loaded right now.</summary>
    public static class SerializerOptions
    {
        private static readonly HashSet<string> None = new();
        private static ICollection<string> _current = None;

        public static bool IsOn(string option) => _current.Contains(option);

        internal static Scope Use(ICollection<string> options)
        {
            var scope = new Scope(_current);
            _current = options ?? None;
            return scope;
        }

        internal readonly struct Scope : IDisposable
        {
            private readonly ICollection<string> _previous;

            public Scope(ICollection<string> previous) => _previous = previous;

            public void Dispose() => _current = _previous;
        }
    }
}
