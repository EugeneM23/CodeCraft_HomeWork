using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

namespace AIKit
{
    /// <summary>
    /// The fields that join graph elements: a node or an edge held by another (a state's tasks, a composite's children,
    /// an edge's target), or a Unity asset a node points to (a subgraph). They are the graph's structure — a graph asset
    /// keeps them as links by id, never inside a node's data — so every such field is [NonSerialized] and public (or
    /// [SerializeField]). Found once per type by reflection; a renamed field keeps its links with [FormerlySerializedAs].
    /// </summary>
    public static class GraphFields
    {
        /// <summary>One link field of a type.</summary>
        public sealed class Link
        {
            public FieldInfo Field;

            /// <summary>The name the asset stores (the field's name).</summary>
            public string Name;

            /// <summary>Earlier names ([FormerlySerializedAs]) an old asset may still use.</summary>
            public string[] Aliases;

            /// <summary>A list of elements rather than one.</summary>
            public bool IsList;

            /// <summary>Points to a Unity asset (by index in the graph asset's object table), not to an element.</summary>
            public bool IsObject;

            /// <summary>The type one entry holds.</summary>
            public Type ElementType;

            public bool Holds(Type type) => type.IsAssignableFrom(ElementType);

            public bool Answers(string name)
            {
                if (name == Name)
                    return true;

                for (int i = 0; i < Aliases.Length; i++)
                {
                    if (Aliases[i] == name)
                        return true;
                }

                return false;
            }

            /// <summary>The entries the field holds now (one or the list's).</summary>
            public IEnumerable<object> Values(object owner)
            {
                object value = Field.GetValue(owner);
                if (!IsList)
                {
                    if (value != null)
                        yield return value;
                    yield break;
                }

                if (value is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                        yield return list[i];
                }
            }
        }

        private static readonly Dictionary<Type, Link[]> Cache = new();
        private static readonly Dictionary<Type, (string Old, string New)[]> Renames = new();

        /// <summary>
        /// A node's data saved under old field names, with the names of today: JsonUtility does not read
        /// [FormerlySerializedAs], so a key the type knows only as a former name is renamed (when the new one is absent).
        /// Only the node's own keys — those of the top object: a key of the same name inside a nested object (an output
        /// of a list, a value) is that object's, and stays.
        /// </summary>
        public static string RenameOldKeys(Type type, string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            (string Old, string New)[] renames;
            lock (Renames)
            {
                if (!Renames.TryGetValue(type, out renames))
                {
                    var found = new List<(string, string)>();
                    for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
                    {
                        const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                                   BindingFlags.DeclaredOnly;
                        foreach (FieldInfo field in current.GetFields(FLAGS))
                        {
                            foreach (var former in field.GetCustomAttributes<FormerlySerializedAsAttribute>())
                                found.Add((former.oldName, field.Name));
                        }
                    }

                    renames = found.ToArray();
                    Renames[type] = renames;
                }
            }

            if (renames.Length == 0)
                return json;

            List<(string Key, int At)> keys = TopKeys(json);
            var replaced = new List<(int At, string Old, string New)>();
            foreach ((string oldName, string newName) in renames)
            {
                int at = keys.FindIndex(key => key.Key == oldName);
                if (at >= 0 && !keys.Exists(key => key.Key == newName))
                    replaced.Add((keys[at].At, oldName, newName));
            }

            // From the end, so the places of the keys before stay where they were
            replaced.Sort((left, right) => right.At.CompareTo(left.At));
            foreach ((int at, string oldName, string newName) in replaced)
                json = json.Substring(0, at) + "\"" + newName + "\"" + json.Substring(at + oldName.Length + 2);

            return json;
        }

        // The keys of the top object of the JSON, each with the index of its opening quote: a string read at depth 1
        // right after '{' or ',' and followed by ':'
        private static List<(string Key, int At)> TopKeys(string json)
        {
            var keys = new List<(string, int)>();
            int depth = 0;
            char last = '\0';                               // the last character outside strings that is not a blank
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int start = i;
                    var text = new System.Text.StringBuilder();
                    for (i++; i < json.Length && json[i] != '"'; i++)
                    {
                        if (json[i] == '\\' && i + 1 < json.Length)
                            i++;
                        text.Append(json[i]);
                    }

                    if (depth == 1 && (last == '{' || last == ','))
                    {
                        int next = i + 1;
                        while (next < json.Length && char.IsWhiteSpace(json[next]))
                            next++;
                        if (next < json.Length && json[next] == ':')
                            keys.Add((text.ToString(), start));
                    }

                    last = '"';
                    continue;
                }

                if (char.IsWhiteSpace(c))
                    continue;

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                    depth--;
                last = c;
            }

            return keys;
        }

        /// <summary>A node or an edge: what a graph asset keeps as a record.</summary>
        public static bool IsElement(Type type) => typeof(AgentNode).IsAssignableFrom(type) || type == typeof(Transition);

        public static Link[] LinksOf(Type type)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue(type, out Link[] links))
                    return links;

                links = Collect(type);
                Cache[type] = links;
                return links;
            }
        }

        public static Link Find(Type type, string name)
        {
            Link[] links = LinksOf(type);
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Answers(name))
                    return links[i];
            }

            return null;
        }

        private static Link[] Collect(Type type)
        {
            var result = new List<Link>();
            var hierarchy = new List<Type>();
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
                hierarchy.Insert(0, current);           // base fields first: a stable order for every subclass

            foreach (Type level in hierarchy)
            {
                const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.DeclaredOnly;
                foreach (FieldInfo field in level.GetFields(FLAGS))
                {
                    if (field.IsInitOnly || (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null))
                        continue;

                    Type fieldType = field.FieldType;
                    bool isList = fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>);
                    Type element = isList ? fieldType.GetGenericArguments()[0] : fieldType;
                    bool isObject = typeof(UnityEngine.Object).IsAssignableFrom(element);
                    if (!isObject && !IsElement(element))
                        continue;

                    var aliases = new List<string>();
                    foreach (var former in field.GetCustomAttributes<FormerlySerializedAsAttribute>())
                        aliases.Add(former.oldName);

                    result.Add(new Link
                    {
                        Field = field,
                        Name = field.Name,
                        Aliases = aliases.ToArray(),
                        IsList = isList,
                        IsObject = isObject,
                        ElementType = element
                    });
                }
            }

            return result.ToArray();
        }
    }
}
