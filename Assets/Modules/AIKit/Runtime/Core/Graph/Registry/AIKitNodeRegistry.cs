using System;
using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// Every graph node type known to the running code, found by reflection once: by stable id (loading a graph
    /// asset) and as a list (the editor's menu). Nodes of other assemblies — a game's own tasks — are found the same
    /// way, as long as they carry [AIKitNode].
    /// </summary>
    public static class AIKitNodeRegistry
    {
        public readonly struct Entry
        {
            public readonly Type Type;
            public readonly AIKitNodeAttribute Info;

            public Entry(Type type, AIKitNodeAttribute info)
            {
                Type = type;
                Info = info;
            }
        }

        private static List<Entry> _entries;
        private static Dictionary<string, Type> _byId;

        public static IReadOnlyList<Entry> Entries
        {
            get
            {
                Build();
                return _entries;
            }
        }

        /// <summary>The node type saved under this id; null — unknown (a removed node, a missing assembly).</summary>
        public static Type Find(string id)
        {
            Build();
            return id != null && _byId.TryGetValue(id, out Type type) ? type : null;
        }

        /// <summary>A fresh node of the type saved under this id (its field defaults), or null.</summary>
        public static AgentNode Create(string id)
        {
            Type type = Find(id);
            return type != null ? (AgentNode)Activator.CreateInstance(type) : null;
        }

        private static void Build()
        {
            if (_entries != null)
                return;

            var entries = new List<Entry>();
            var byId = new Dictionary<string, Type>();
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in TypesOf(assembly))
                {
                    if (type == null || type.IsAbstract || !typeof(AgentNode).IsAssignableFrom(type))
                        continue;

                    var info = (AIKitNodeAttribute)Attribute.GetCustomAttribute(type, typeof(AIKitNodeAttribute));
                    if (info == null)
                        continue;

                    EnsureUnique(byId, info.Id, type);
                    byId.Add(info.Id, type);
                    entries.Add(new Entry(type, info));
                }
            }

            entries.Sort((a, b) => string.CompareOrdinal(a.Info.Path, b.Info.Path));
            _entries = entries;
            _byId = byId;
        }

        // An assembly that cannot load some of its types still gives the ones it loaded; no test assembly fails that way
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private static Type[] TypesOf(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException exception)
            {
                return exception.Types;
            }
        }

        // Two node classes with one id is a programming error, caught the first time the registry builds — never in a
        // project that compiles its node set cleanly, so there is nothing to cover
        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private static void EnsureUnique(Dictionary<string, Type> byId, string id, Type type)
        {
            if (byId.TryGetValue(id, out Type other))
                throw new InvalidOperationException($"AI Kit node id '{id}' is used by both {other.FullName} and {type.FullName}");
        }
    }
}
