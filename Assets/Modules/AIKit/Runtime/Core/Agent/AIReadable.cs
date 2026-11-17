using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A name of the game's own for a member a graph reads off a component (Try Get Component), or a member the graph may read that is not public
    /// (step 91). Any component can be looked for and every public number, flag and vector of it read without it (Rigidbody's
    /// mass, Transform's position); the mark gives a member its own name in the graph's lists ([AIReadable("Stamina")]),
    /// lets a private field or property be read, and on a class — puts it first in the lists.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class AIReadableAttribute : Attribute
    {
        public AIReadableAttribute()
        {
        }

        /// <summary>A member under a name of its own in the graph's lists ("Stamina" for _stamina).</summary>
        public AIReadableAttribute(string name) => Name = name;

        /// <summary>The name the graph shows; null — the member's own, in words.</summary>
        public string Name { get; }
    }

    /// <summary>One value of a component a graph may read: a number (a vector gives one per axis and its length) or a flag.</summary>
    public sealed class ReadableMember
    {
        private readonly Func<Component, float> _number;
        private readonly Func<Component, bool> _flag;

        internal ReadableMember(string name, string label, Func<Component, float> number, Func<Component, bool> flag)
        {
            Name = name;
            Label = label;
            _number = number;
            _flag = flag;
        }

        /// <summary>What a graph keeps: the member's own name ("mass"), a vector's axis after a dot ("linearVelocity.y").</summary>
        public string Name { get; }

        /// <summary>What the graph's lists show ("Mass", "Linear Velocity Y", "Stamina").</summary>
        public string Label { get; }

        /// <summary>A flag (bool), not a number.</summary>
        public bool IsFlag => _flag != null;

        /// <summary>Reads the number off the component; false — it is a flag, the component is gone, or reading it failed.</summary>
        public bool TryNumber(Component component, out float value)
        {
            value = 0f;
            if (_number == null || component == null)
                return false;

            try
            {
                value = _number(component);
                return true;
            }
            catch (Exception)
            {
                return false;                           // a property that cannot be read now (an engine's rule): no data
            }
        }

        /// <summary>Reads the flag off the component; false — it is a number, the component is gone, or reading it failed.</summary>
        public bool TryFlag(Component component, out bool value)
        {
            value = false;
            if (_flag == null || component == null)
                return false;

            try
            {
                value = _flag(component);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// What a graph may read of a component (step 91): every public number (float, int, double), flag (bool) and vector
    /// (Vector2, Vector3 — each axis and the length) of it and of its base classes up to Behaviour, not obsolete, plus what
    /// the game marked [AIReadable] (any visibility, its own name). Found once per type; the type's own members first. A
    /// property is read by a delegate (no allocation, handbook C.3); a field through reflection (a boxed value — a Get keeps
    /// its answer for the think).
    /// </summary>
    public static class AIReadables
    {
        private const string LENGTH = "magnitude";
        private static readonly string[] Axes = { "x", "y", "z" };

        private static readonly Dictionary<Type, ReadableMember[]> Cache = new();
        private static readonly Dictionary<string, Type> Named = new();

        /// <summary>The values of the type a graph may read, the type's own first.</summary>
        public static IReadOnlyList<ReadableMember> MembersOf(Type type)
        {
            if (type == null || !typeof(Component).IsAssignableFrom(type))
                return Array.Empty<ReadableMember>();

            lock (Cache)
            {
                if (Cache.TryGetValue(type, out ReadableMember[] members))
                    return members;

                members = Collect(type);
                Cache[type] = members;
                return members;
            }
        }

        /// <summary>The value of the type by the name a graph keeps; null — none it may read.</summary>
        public static ReadableMember Find(Type type, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            IReadOnlyList<ReadableMember> members = MembersOf(type);
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Name == name)
                    return members[i];
            }

            return null;
        }

        /// <summary>A component type a Try Get Component may look for (any component).</summary>
        public static bool IsComponentType(Type type) =>
            type != null && typeof(Component).IsAssignableFrom(type) && !type.IsGenericTypeDefinition;

        /// <summary>The game marked the type [AIReadable] (it or a base class): its lists put it first.</summary>
        public static bool IsMarked(Type type) => type != null && type.IsDefined(typeof(AIReadableAttribute), true);

        /// <summary>The type by its full name in any loaded assembly (a game's own components too); null — none.</summary>
        public static Type TypeNamed(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            lock (Named)
            {
                if (Named.TryGetValue(name, out Type known))
                    return known;

                Type type = Type.GetType(name);
                if (type == null)
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = assembly.GetType(name);
                        if (type != null)
                            break;
                    }
                }

                Named[name] = type;
                return type;
            }
        }

        /// <summary>The type's name in words, as the canvas says it: "Player Controller".</summary>
        public static string Words(Type type) => type == null ? "?" : Spaced(type.Name);

        /// <summary>A type's full name in words ("Game.PlayerController" — "Player Controller"); empty — "?".</summary>
        public static string Words(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return "?";

            int dot = fullName.LastIndexOfAny(new[] { '.', '+' });
            return Spaced(dot >= 0 ? fullName.Substring(dot + 1) : fullName);
        }

        /// <summary>A member's name in words: "_isBlocking" — "Is Blocking", "maxHP" — "Max HP", "linearVelocity.y" — "Linear Velocity Y".</summary>
        public static string Spaced(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            int dot = name.IndexOf('.');
            if (dot > 0)
            {
                string axis = name.Substring(dot + 1);
                return $"{Spaced(name.Substring(0, dot))} {(axis == LENGTH ? "Length" : axis.ToUpperInvariant())}";
            }

            name = name.TrimStart('_');
            if (name.Length > 2 && name[0] == 'm' && name[1] == '_')
                name = name.Substring(2);

            var words = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i == 0)
                {
                    words.Append(char.ToUpperInvariant(c));
                    continue;
                }

                bool upper = char.IsUpper(c);
                bool afterLower = char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]);
                bool beforeLower = i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1]);
                if (upper && (afterLower || beforeLower) || c == '_')
                    words.Append(' ');
                if (c != '_')
                    words.Append(c);
            }

            return words.ToString();
        }

        // ---------- inside ----------

        // The levels whose public members say nothing about the object (its name, tag, flags of the engine)
        private static bool IsEngineBase(Type level) =>
            level == typeof(UnityEngine.Object) || level == typeof(Component) || level == typeof(MonoBehaviour);

        private static ReadableMember[] Collect(Type type)
        {
            var result = new List<ReadableMember>();
            var names = new HashSet<string>();
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.DeclaredOnly;

            // The type's own first, then its bases (Behaviour's enabled last)
            for (Type level = type; level != null && level != typeof(object); level = level.BaseType)
            {
                bool engineBase = IsEngineBase(level);
                foreach (FieldInfo field in level.GetFields(FLAGS))
                {
                    var mark = field.GetCustomAttribute<AIReadableAttribute>();
                    if (!Offered(field.IsPublic, mark, engineBase, field) || !names.Add(field.Name))
                        continue;

                    Add(result, field.Name, mark?.Name, field.FieldType, FieldGetter(field));
                }

                foreach (PropertyInfo property in level.GetProperties(FLAGS))
                {
                    var mark = property.GetCustomAttribute<AIReadableAttribute>();
                    MethodInfo getter = property.GetGetMethod(true);
                    if (getter == null || getter.IsStatic || property.GetIndexParameters().Length > 0 ||
                        !Offered(getter.IsPublic, mark, engineBase, property) || !names.Add(property.Name))
                        continue;

                    Add(result, property.Name, mark?.Name, property.PropertyType, PropertyGetter(level, property, getter));
                }
            }

            return result.ToArray();
        }

        // A public member (of a level that says something about the object, not obsolete), or one the game marked
        private static bool Offered(bool isPublic, AIReadableAttribute mark, bool engineBase, MemberInfo member)
        {
            if (mark != null)
                return true;

            return isPublic && !engineBase && !member.IsDefined(typeof(ObsoleteAttribute), true) &&
                   !member.Name.StartsWith("<", StringComparison.Ordinal);
        }

        // One member: a number or a flag as it is, a vector as its axes and its length; another kind is not read
        private static void Add(List<ReadableMember> result, string name, string label, Type kind, Delegate getter)
        {
            switch (getter)
            {
                case Func<Component, float> number:
                    result.Add(new ReadableMember(name, label ?? Spaced(name), number, null));
                    break;
                case Func<Component, bool> flag:
                    result.Add(new ReadableMember(name, label ?? Spaced(name), null, flag));
                    break;
                case Func<Component, Vector3> vector:
                    int axes = kind == typeof(Vector2) ? 2 : 3;
                    for (int axis = 0; axis < axes; axis++)
                    {
                        int at = axis;
                        string axisName = $"{name}.{Axes[axis]}";
                        result.Add(new ReadableMember(axisName, label != null ? $"{label} {Axes[axis].ToUpperInvariant()}" : Spaced(axisName),
                            component => vector(component)[at], null));
                    }

                    string length = $"{name}.{LENGTH}";
                    result.Add(new ReadableMember(length, label != null ? $"{label} Length" : Spaced(length),
                        component => vector(component).magnitude, null));
                    break;
            }
        }

        // ---------- getters ----------

        private static bool IsNumber(Type type) => type == typeof(float) || type == typeof(int) || type == typeof(double);

        private static bool IsVector(Type type) => type == typeof(Vector3) || type == typeof(Vector2);

        // A field through reflection: the value comes boxed
        private static Delegate FieldGetter(FieldInfo field)
        {
            Type kind = field.FieldType;
            if (kind == typeof(bool))
                return new Func<Component, bool>(component => (bool)field.GetValue(component));
            if (IsNumber(kind))
                return new Func<Component, float>(component => Convert.ToSingle(field.GetValue(component)));
            if (kind == typeof(Vector3))
                return new Func<Component, Vector3>(component => (Vector3)field.GetValue(component));
            if (kind == typeof(Vector2))
                return new Func<Component, Vector3>(component => (Vector2)field.GetValue(component));
            return null;
        }

        // A property by a delegate made for its getter (no allocation); one no delegate can be made for — by reflection
        private static Delegate PropertyGetter(Type owner, PropertyInfo property, MethodInfo getter)
        {
            Type kind = property.PropertyType;
            string helper = kind == typeof(bool) ? nameof(BoolOf)
                : kind == typeof(float) ? nameof(FloatOf)
                : kind == typeof(int) ? nameof(IntOf)
                : kind == typeof(double) ? nameof(DoubleOf)
                : kind == typeof(Vector3) ? nameof(Vector3Of)
                : kind == typeof(Vector2) ? nameof(Vector2Of)
                : null;
            if (helper == null)
                return null;

            if (typeof(Component).IsAssignableFrom(owner))
            {
                try
                {
                    MethodInfo method = typeof(AIReadables).GetMethod(helper, BindingFlags.NonPublic | BindingFlags.Static);
                    if (method?.MakeGenericMethod(owner).Invoke(null, new object[] { getter }) is Delegate made)
                        return made;
                }
                catch (Exception)
                {
                    // falls back to reflection
                }
            }

            if (kind == typeof(bool))
                return new Func<Component, bool>(component => (bool)property.GetValue(component));
            if (IsNumber(kind))
                return new Func<Component, float>(component => Convert.ToSingle(property.GetValue(component)));
            if (kind == typeof(Vector3))
                return new Func<Component, Vector3>(component => (Vector3)property.GetValue(component));
            return new Func<Component, Vector3>(component => (Vector2)property.GetValue(component));
        }

        private static Func<Component, float> FloatOf<TOwner>(MethodInfo getter) where TOwner : Component
        {
            var read = (Func<TOwner, float>)Delegate.CreateDelegate(typeof(Func<TOwner, float>), getter);
            return component => read((TOwner)component);
        }

        private static Func<Component, float> IntOf<TOwner>(MethodInfo getter) where TOwner : Component
        {
            var read = (Func<TOwner, int>)Delegate.CreateDelegate(typeof(Func<TOwner, int>), getter);
            return component => read((TOwner)component);
        }

        private static Func<Component, float> DoubleOf<TOwner>(MethodInfo getter) where TOwner : Component
        {
            var read = (Func<TOwner, double>)Delegate.CreateDelegate(typeof(Func<TOwner, double>), getter);
            return component => (float)read((TOwner)component);
        }

        private static Func<Component, bool> BoolOf<TOwner>(MethodInfo getter) where TOwner : Component
        {
            var read = (Func<TOwner, bool>)Delegate.CreateDelegate(typeof(Func<TOwner, bool>), getter);
            return component => read((TOwner)component);
        }

        private static Func<Component, Vector3> Vector3Of<TOwner>(MethodInfo getter) where TOwner : Component
        {
            var read = (Func<TOwner, Vector3>)Delegate.CreateDelegate(typeof(Func<TOwner, Vector3>), getter);
            return component => read((TOwner)component);
        }

        private static Func<Component, Vector3> Vector2Of<TOwner>(MethodInfo getter) where TOwner : Component
        {
            var read = (Func<TOwner, Vector2>)Delegate.CreateDelegate(typeof(Func<TOwner, Vector2>), getter);
            return component => read((TOwner)component);
        }
    }
}
