using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Saving.Objects
{
    /// <summary>
    /// Which members of a type a save sees. <see cref="Marked"/>: [Save] fields and properties. <see cref="All"/>
    /// (a registered object): the [Save] members if there are any; otherwise for your scripts
    /// the fields Unity serializes (public, [SerializeField]), for Unity's own components (the engine's, uGUI, TextMesh
    /// Pro…) their public properties, for plain classes public fields and properties — always without values that hold
    /// Unity objects anywhere inside (a reference, a list of them, a wave class with its prefab).
    /// <see cref="AllWithReferences"/> (the All Fields mode) adds those for scripts: saved when the save can express
    /// them, left alone otherwise. <see cref="Candidates"/> (the Selected mode): any field or
    /// property with a getter and a setter whose type can be saved, references included. Members that make copies or
    /// fight the engine (material, mesh, aspect…) never show. Reflection runs once per type.
    /// </summary>
    public static class SaveMembers
    {
        private const BindingFlags DECLARED =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // Properties of Unity components that must not be written back: they make copies, freeze automatic values
        // or depend on the screen; enabled is saved on its own. Camera's layerCullSpherical and stereoTargetEye and
        // Light's shadowResolution belong to the built-in renderer only: in URP and HDRP every read and write logs a
        // warning
        private static readonly HashSet<string> Risky = new(StringComparer.Ordinal)
        {
            "enabled", "tag", "name", "hideFlags", "runInEditMode", "useGUILayout", "material", "materials", "mesh",
            "parent", "hasChanged", "hierarchyCapacity", "aspect", "pixelRect", "bounds", "localBounds", "centerOfMass",
            "inertiaTensor", "inertiaTensorRotation", "inertia", "bodyPosition", "bodyRotation", "rootPosition",
            "rootRotation", "playbackTime", "recorderStartTime", "recorderStopTime", "time", "timeSamples", "density",
            "layerCullSpherical", "stereoTargetEye", "shadowResolution"
        };

        private static readonly HashSet<Type> SimpleStructs = new()
        {
            typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Vector2Int), typeof(Vector3Int), typeof(Quaternion),
            typeof(Color), typeof(Color32), typeof(Rect), typeof(Bounds), typeof(LayerMask)
        };

        private static readonly Dictionary<Type, IReadOnlyList<SaveMember>> MarkedCache = new();
        private static readonly Dictionary<Type, IReadOnlyList<SaveMember>> AllCache = new();
        private static readonly Dictionary<Type, IReadOnlyList<SaveMember>> AllWithReferencesCache = new();
        private static readonly Dictionary<Type, IReadOnlyList<SaveMember>> CandidatesCache = new();
        private static readonly Dictionary<Type, IReadOnlyList<SaveMember>> AllWithPrivateCache = new();
        private static readonly Dictionary<Type, IReadOnlyList<SaveMember>> StateCache = new();

        /// <summary>Forgets every type looked at: tests that need the first-look warnings again.</summary>
        internal static void ForgetTypes()
        {
            MarkedCache.Clear();
            AllCache.Clear();
            AllWithReferencesCache.Clear();
            AllWithPrivateCache.Clear();
            StateCache.Clear();
            CandidatesCache.Clear();
            HoldsCache.Clear();
        }

        public static IReadOnlyList<SaveMember> Marked(Type type) => Cached(MarkedCache, type, FindMarked);
        public static IReadOnlyList<SaveMember> All(Type type) => Cached(AllCache, type, FindValues);

        /// <summary>
        /// What the All Fields mode of a Saveable keeps: <see cref="All"/> plus, for your scripts, the members that hold
        /// Unity objects (<see cref="SaveMember.IsReference"/>) — a target, an owner, a list of minions. Such a member is
        /// saved only when the save can express everything in it (Saveables, their children and components, prefabs
        /// with a Saveable, assets of the settings' folders); otherwise it is left out and a load keeps what the scene
        /// has there, so wiring from the Inspector is never wiped.
        /// </summary>
        public static IReadOnlyList<SaveMember> AllWithReferences(Type type) =>
            Cached(AllWithReferencesCache, type, FindValuesAndReferences);

        /// <summary>
        /// <see cref="AllWithReferences"/> plus, for your scripts, the private fields Unity does not serialize that
        /// hold one simple value (a number, a flag, an enum, a string, a vector): the state a script keeps to itself —
        /// a look angle it writes to a Transform every frame, a phase, a counter. What the whole-scene scope takes
        /// when its Private Fields box is checked. Not for a type with [Save] members (its author chose), not
        /// references or collections (caches found in Start), not [NonSerialized], readonly or const. A number named
        /// like a moment of Time.time is saved as a moment (<see cref="SaveMember.IsMoment"/>).
        /// </summary>
        public static IReadOnlyList<SaveMember> AllWithPrivate(Type type) =>
            Cached(AllWithPrivateCache, type, FindValuesReferencesAndPrivate);

        /// <summary>
        /// What a plain object handed over by an ISaveStateProvider keeps (a class of a DI container: health, a clip):
        /// its [Save] members when it has any; otherwise its public values (<see cref="All"/>) and the simple values it
        /// holds privately — numbers, flags, enums, strings, vectors in fields and auto-properties. What it was given
        /// (readonly fields, references to services, lists) is left alone.
        /// </summary>
        public static IReadOnlyList<SaveMember> State(Type type) => Cached(StateCache, type, FindState);

        public static IReadOnlyList<SaveMember> Candidates(Type type) => Cached(CandidatesCache, type, FindCandidates);

        /// <summary>Unity's own component (engine, uGUI, packages): saved through its public properties, not its fields.</summary>
        public static bool UsesProperties(Type type)
        {
            if (!typeof(Object).IsAssignableFrom(type))
                return false;

            bool script = typeof(MonoBehaviour).IsAssignableFrom(type) || typeof(ScriptableObject).IsAssignableFrom(type);
            return !script || IsUnityAssembly(type.Assembly) || IsThirdParty(type.Assembly);
        }

        /// <summary>
        /// A value of the type holds Unity objects anywhere inside: directly, in a collection or a dictionary, in the
        /// fields of a class or struct (a wave with its prefab). Saving such a value would save references.
        /// </summary>
        public static bool HoldsUnityObjects(Type type)
        {
            if (type == null)
                return false;

            if (!HoldsCache.TryGetValue(type, out bool holds))
            {
                holds = Holds(type, new HashSet<Type>());
                HoldsCache[type] = holds;
            }

            return holds;
        }

        private static readonly Dictionary<Type, bool> HoldsCache = new();

        private static bool Holds(Type type, HashSet<Type> seen)
        {
            if (typeof(Object).IsAssignableFrom(type))
                return true;

            if (type.IsPrimitive || type.IsEnum || type.IsPointer || type == typeof(string) || !seen.Add(type))
                return false;

            if (type.IsArray)
                return Holds(type.GetElementType(), seen);

            if (type.IsGenericType && type.GetGenericArguments().Any(argument => Holds(argument, seen)))
                return true;

            string space = type.Namespace ?? string.Empty;
            if (space == "System" || space.StartsWith("System.", StringComparison.Ordinal) || space == "UnityEngine"
                || space.StartsWith("UnityEngine.", StringComparison.Ordinal))
                return false;

            // Every instance field: backing fields of properties too, so plain objects are covered as well
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(DECLARED))
                {
                    if (Holds(field.FieldType, seen))
                        return true;
                }
            }

            return false;
        }

        // ---------- Finding ----------

        private static IReadOnlyList<SaveMember> FindMarked(Type type)
        {
            var members = new List<SaveMember>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type current in Chain(type))
            {
                foreach (FieldInfo field in current.GetFields(DECLARED))
                {
                    var attribute = field.GetCustomAttribute<SaveAttribute>();
                    // readonly fields are skipped; static never comes with these flags
                    if (attribute == null || field.IsInitOnly)
                        continue;

                    AddMarked(type, current, new SaveMember(field, attribute.Key), members, keys);
                }

                foreach (PropertyInfo property in current.GetProperties(DECLARED))
                {
                    // Declared here only (no inherit): an override and its base declaration are one property, taken once
                    var attribute = property.GetCustomAttribute<SaveAttribute>(inherit: false);
                    if (attribute == null || !properties.Add(property.Name))
                        continue;

                    if (!IsReadWrite(property, publicOnly: false))
                    {
                        Debug.LogWarning($"[Save] on {current.Name}.{property.Name} is skipped: a saved property needs a getter " +
                                         "and a setter and no index.");
                        continue;
                    }

                    AddMarked(type, current, new SaveMember(property, attribute.Key), members, keys);
                }
            }

            return members;
        }

        private static void AddMarked(Type type, Type declaring, SaveMember member, List<SaveMember> members, HashSet<string> keys)
        {
            if (keys.Add(member.Key))
                members.Add(member);
            else
                Debug.LogError($"[Save] key '{member.Key}' is used twice in {type.Name}; {declaring.Name}.{member.Name} is skipped.");
        }

        private static IReadOnlyList<SaveMember> FindValues(Type type) => FindAll(type, references: false);

        private static IReadOnlyList<SaveMember> FindValuesAndReferences(Type type) => FindAll(type, references: true);

        private static IReadOnlyList<SaveMember> FindValuesReferencesAndPrivate(Type type)
        {
            IReadOnlyList<SaveMember> members = AllWithReferences(type);
            bool script = typeof(Object).IsAssignableFrom(type) && !UsesProperties(type);
            if (!script || Marked(type).Count > 0)
                return members;

            var all = new List<SaveMember>(members);
            var names = new HashSet<string>(members.Select(member => member.Name), StringComparer.Ordinal);
            foreach (Type current in OwnChain(type))
            {
                foreach (FieldInfo field in current.GetFields(DECLARED))
                {
                    // Backing fields of auto-properties are in already, as the properties
                    if (!field.IsInitOnly && !field.IsLiteral && !IsUnitySerialized(field)
                        && !field.IsDefined(typeof(NonSerializedAttribute)) && !field.IsDefined(typeof(CompilerGeneratedAttribute))
                        && IsOneSimpleValue(field.FieldType) && names.Add(field.Name))
                    {
                        bool moment = LooksLikeAMoment(field.Name, field.FieldType);
                        if (moment || (!LooksLikeWhatWasSeenLast(field.Name, field.FieldType) && !LooksLikeASetupFlag(field.Name, field.FieldType)))
                            all.Add(new SaveMember(field, null, moment));
                    }
                }
            }

            return all;
        }

        private static IReadOnlyList<SaveMember> FindState(Type type)
        {
            IReadOnlyList<SaveMember> members = All(type);
            if (Marked(type).Count > 0 || typeof(Object).IsAssignableFrom(type))
                return members;

            var all = new List<SaveMember>(members);
            var names = new HashSet<string>(members.Select(member => member.Name), StringComparer.Ordinal);
            foreach (Type current in OwnChain(type))
            {
                foreach (PropertyInfo property in current.GetProperties(DECLARED))
                {
                    if (IsAutoProperty(current, property) && IsReadWrite(property, publicOnly: false) && !IsSetupFlag(property)
                        && IsOneSimpleValue(property.PropertyType) && names.Add(property.Name))
                        all.Add(new SaveMember(property, null, LooksLikeAMoment(property.Name, property.PropertyType)));
                }

                foreach (FieldInfo field in current.GetFields(DECLARED))
                {
                    if (field.IsInitOnly || field.IsLiteral || field.IsDefined(typeof(NonSerializedAttribute))
                        || field.IsDefined(typeof(CompilerGeneratedAttribute)) || !IsOneSimpleValue(field.FieldType)
                        || !names.Add(field.Name))
                        continue;

                    bool moment = LooksLikeAMoment(field.Name, field.FieldType);
                    if (moment || (!LooksLikeWhatWasSeenLast(field.Name, field.FieldType) && !LooksLikeASetupFlag(field.Name, field.FieldType)))
                        all.Add(new SaveMember(field, null, moment));
                }
            }

            return all;
        }

        private static readonly string[] SeenLast = { "last", "prev", "old", "cached", "was" };

        /// <summary>
        /// A private number or flag named like what the script saw the last time it looked (m_LastAmmoRatio,
        /// _prevState, _wasGrounded): scripts compare it with the value of now and bring their looks up to date only
        /// when the two differ — a glow of a material, the rate of a particle system, a label. Those looks are not in
        /// a save, so with the field back the script would see no change and leave them as the scene has them. Left
        /// out, the script notices the difference in its first frame, as it does when the scene starts. A place or a
        /// rotation (m_LastRootPosition, m_PreviousAim) is where the object was and is saved.
        /// </summary>
        private static bool LooksLikeWhatWasSeenLast(string name, Type type)
        {
            if (!type.IsPrimitive && !type.IsEnum && type != typeof(string))
                return false;

            string bare = name.TrimStart('_');
            if (bare.StartsWith("m_", StringComparison.OrdinalIgnoreCase))
                bare = bare.Substring(2);

            foreach (string word in SeenLast)
            {
                if (bare.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static readonly string[] SetupWords = { "init", "setup", "subscrib" };

        /// <summary>
        /// A private flag that says the script has done its own setup (isInitialized, _inited, _isSetup,
        /// m_Subscribed): a fact about this instance's life, not about the game. What the flag guards — references
        /// found in the setup, event subscriptions — is not in a save, so with the flag back on an object that was
        /// just made the script would skip the setup and run on nothing. Left out, a new object sets itself up as it
        /// does when the scene starts, and an object that is alive keeps what it has done.
        /// Only words that mean setup and nothing else: "started", "awake", "built", "destroyed" are as often the
        /// state of the game (a race that has started, a guard that is awake).
        /// </summary>
        private static bool LooksLikeASetupFlag(string name, Type type)
        {
            if (type != typeof(bool))
                return false;

            foreach (string word in SetupWords)
            {
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>The same flag kept in an auto-property only the script itself sets ({ get; private set; }).</summary>
        private static bool IsSetupFlag(PropertyInfo property) =>
            property.SetMethod is { IsPublic: false } && LooksLikeASetupFlag(property.Name, property.PropertyType);

        // A number with "time" in its name that counts seconds instead of marking a moment
        private static readonly string[] Durations =
        {
            "timer", "timeout", "lifetime", "playtime", "elapsed", "total", "remain", "left", "delta", "accum",
            "duration", "cooldown", "interval", "delay", "scale", "step", "played", "spent"
        };

        /// <summary>
        /// A number a script keeps to itself, named like a moment of Time.time (m_LastTimeShot, _startTime,
        /// TimeLastSeenTarget) and not like a count of seconds (…Timer, _elapsedTime, _timeLeft). Only for members
        /// Unity does not serialize: a public "MaxLifeTime" is a setting. See <see cref="SaveMember.IsMoment"/>.
        /// </summary>
        private static bool LooksLikeAMoment(string name, Type type)
        {
            if ((type != typeof(float) && type != typeof(double)) || name.IndexOf("time", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            foreach (string word in Durations)
            {
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            return true;
        }

        private static bool IsOneSimpleValue(Type type) =>
            (type.IsPrimitive && type != typeof(IntPtr) && type != typeof(UIntPtr)) || type.IsEnum || type == typeof(string)
            || SimpleStructs.Contains(type);

        /// <summary>References are taken only for scripts: Unity's own components and plain objects stay values-only.</summary>
        private static IReadOnlyList<SaveMember> FindAll(Type type, bool references)
        {
            IReadOnlyList<SaveMember> marked = Marked(type);
            if (marked.Count > 0)
                return marked;

            if (UsesProperties(type))
                return EngineProperties(type, references: false);

            bool unity = typeof(Object).IsAssignableFrom(type);
            references &= unity;
            var members = new List<SaveMember>();
            foreach (Type current in OwnChain(type))
            {
                foreach (FieldInfo field in current.GetFields(DECLARED))
                {
                    if (IsUnitySerialized(field) && (references || !HoldsUnityObjects(field.FieldType))
                        && IsStorable(field.FieldType, plainOwner: !unity))
                        members.Add(new SaveMember(field, null));
                }
            }

            if (unity)
            {
                // Auto-properties of a script ({ get; set; }, { get; private set; }) are plain state Unity does not
                // serialize — often the very state a game changes (CurrentHealth): no code behind them, safe to write
                var names = new HashSet<string>(members.Select(member => member.Name), StringComparer.Ordinal);
                foreach (Type current in OwnChain(type))
                {
                    foreach (PropertyInfo property in current.GetProperties(DECLARED))
                    {
                        if (IsAutoProperty(current, property) && IsReadWrite(property, publicOnly: false) && !IsSetupFlag(property)
                            && (references || !HoldsUnityObjects(property.PropertyType))
                            && IsStorable(property.PropertyType, plainOwner: false) && names.Add(property.Name))
                            members.Add(new SaveMember(property, null, LooksLikeAMoment(property.Name, property.PropertyType)));
                    }
                }
            }
            else
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (Type current in OwnChain(type))
                {
                    foreach (PropertyInfo property in current.GetProperties(DECLARED))
                    {
                        // An override and its base declaration are one property: the closest one is taken
                        if (IsReadWrite(property, publicOnly: true) && !HoldsUnityObjects(property.PropertyType)
                            && IsStorable(property.PropertyType, plainOwner: true) && names.Add(property.Name))
                            members.Add(new SaveMember(property, null));
                    }
                }
            }

            return members;
        }

        private static IReadOnlyList<SaveMember> FindCandidates(Type type)
        {
            if (UsesProperties(type))
                return EngineProperties(type, references: true);

            bool unity = typeof(Object).IsAssignableFrom(type);
            var members = new List<SaveMember>(Marked(type));
            var names = new HashSet<string>(members.Select(member => member.Name), StringComparer.Ordinal);
            foreach (Type current in Chain(type))
            {
                foreach (FieldInfo field in current.GetFields(DECLARED))
                {
                    if (!field.IsInitOnly && !field.IsLiteral && !field.IsDefined(typeof(CompilerGeneratedAttribute))
                        && IsStorable(field.FieldType, plainOwner: !unity) && names.Add(field.Name))
                        members.Add(new SaveMember(field, null,
                            unity && !IsUnitySerialized(field) && LooksLikeAMoment(field.Name, field.FieldType)));
                }
            }

            foreach (Type current in Chain(type))
            {
                foreach (PropertyInfo property in current.GetProperties(DECLARED))
                {
                    if (IsReadWrite(property, publicOnly: false) && IsStorable(property.PropertyType, plainOwner: !unity)
                        && names.Add(property.Name))
                        members.Add(new SaveMember(property, null,
                            unity && IsAutoProperty(current, property) && LooksLikeAMoment(property.Name, property.PropertyType)));
                }
            }

            return members;
        }

        /// <summary>Public read-write properties of Unity's own components, simple values (and references if asked).</summary>
        private static IReadOnlyList<SaveMember> EngineProperties(Type type, bool references)
        {
            var members = new List<SaveMember>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (Type current in Chain(type))
            {
                foreach (PropertyInfo property in current.GetProperties(DECLARED))
                {
                    if (!IsReadWrite(property, publicOnly: true) || Risky.Contains(property.Name)
                        || property.IsDefined(typeof(ObsoleteAttribute)) || !names.Add(property.Name))
                        continue;

                    Type propertyType = property.PropertyType;
                    if (IsSimple(propertyType) || (references && IsReferenceOrReferences(propertyType)))
                        members.Add(new SaveMember(property, null));
                }
            }

            return members;
        }

        // ---------- Rules ----------

        private static IEnumerable<Type> Chain(Type type)
        {
            for (Type current = type; current != null && !IsChainEnd(current); current = current.BaseType)
                yield return current;
        }

        private static bool IsChainEnd(Type type) =>
            type == typeof(object) || type == typeof(Object) || type == typeof(Component) || type == typeof(Behaviour)
            || type == typeof(MonoBehaviour) || type == typeof(ScriptableObject);

        /// <summary>
        /// The chain without base classes from third-party code: what a kernel or an installer of a framework keeps to
        /// itself (it has run, it has installed) is the framework's to manage, not a script's state to bring back.
        /// </summary>
        private static IEnumerable<Type> OwnChain(Type type)
        {
            bool own = !IsThirdParty(type.Assembly);
            foreach (Type current in Chain(type))
            {
                if (!own || !IsThirdParty(current.Assembly))
                    yield return current;
            }
        }

        private static bool IsUnityAssembly(Assembly assembly)
        {
            string name = assembly.GetName().Name;
            return name.StartsWith("UnityEngine", StringComparison.Ordinal) || name.StartsWith("Unity.", StringComparison.Ordinal);
        }

        private const string FIRST_PASS = "Assembly-CSharp-firstpass";
        private static HashSet<string> _thirdParty = new(StringComparer.Ordinal);

        /// <summary>
        /// Assemblies of code the game did not write: plugins from the Asset Store, DI and tween frameworks, packages
        /// (SaveSystemSettings lists them, the editor keeps the list). Their components are treated like Unity's own:
        /// the whole-scene scope and the All Fields mode do not reach into their fields — a Saveable with a mode set by
        /// hand still saves what is picked on it.
        /// </summary>
        internal static void SetThirdPartyAssemblies(IEnumerable<string> names)
        {
            var next = new HashSet<string>(names ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (next.SetEquals(_thirdParty))
                return;

            _thirdParty = next;
            ForgetTypes();
        }

        internal static IReadOnlyCollection<string> ThirdPartyAssemblies => _thirdParty;

        /// <summary>The type comes from code the game did not write (a plugin, a package) — not from Unity itself.</summary>
        public static bool IsThirdParty(Type type) => type != null && IsThirdParty(type.Assembly);

        private static bool IsThirdParty(Assembly assembly)
        {
            string name = assembly.GetName().Name;
            return name == FIRST_PASS || _thirdParty.Contains(name);
        }

        private static bool IsUnitySerialized(FieldInfo field) =>
            !field.IsInitOnly && !field.IsLiteral && !field.IsDefined(typeof(NonSerializedAttribute))
            && (field.IsPublic || field.IsDefined(typeof(SerializeField)));

        private static bool IsAutoProperty(Type declaring, PropertyInfo property) =>
            declaring.GetField($"<{property.Name}>k__BackingField", DECLARED) is { } backing
            && backing.IsDefined(typeof(CompilerGeneratedAttribute));

        private static bool IsReadWrite(PropertyInfo property, bool publicOnly) =>
            property.GetIndexParameters().Length == 0 && property.GetGetMethod(!publicOnly) != null
            && property.GetSetMethod(!publicOnly) != null;

        private static bool IsSimple(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || SimpleStructs.Contains(type))
                return type != typeof(IntPtr) && type != typeof(UIntPtr);

            Type element = ElementOf(type);
            return element != null && IsSimple(element);
        }

        private static bool IsReferenceOrReferences(Type type) =>
            typeof(Object).IsAssignableFrom(type) || (ElementOf(type) is { } element && typeof(Object).IsAssignableFrom(element));

        /// <summary>
        /// Can a value of the type go through JSON and come back: no delegates, interfaces, abstract or system
        /// classes; classes of a Unity object only when [Serializable], as Unity itself wants.
        /// </summary>
        private static bool IsStorable(Type type, bool plainOwner)
        {
            if (type.IsPointer || type.IsByRef || type == typeof(object) || typeof(Delegate).IsAssignableFrom(type))
                return false;

            if (typeof(Object).IsAssignableFrom(type))
                return true;

            if (type.IsPrimitive || type.IsEnum)
                return type != typeof(IntPtr) && type != typeof(UIntPtr);

            if (type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) || type == typeof(TimeSpan)
                || type == typeof(Guid))
                return true;

            if (type.IsArray)
                return type.GetArrayRank() == 1 && IsStorable(type.GetElementType(), plainOwner);

            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                Type[] arguments = type.GetGenericArguments();
                if (definition == typeof(List<>) || definition == typeof(HashSet<>) || definition == typeof(Nullable<>))
                    return IsStorable(arguments[0], plainOwner);

                if (definition == typeof(Dictionary<,>))
                    return (arguments[0] == typeof(string) || arguments[0].IsPrimitive || arguments[0].IsEnum)
                           && IsStorable(arguments[1], plainOwner);
            }

            if (type.IsInterface || type.IsAbstract || type.ContainsGenericParameters)
                return false;

            string space = type.Namespace ?? string.Empty;
            if (space == "System" || space.StartsWith("System.", StringComparison.Ordinal))
                return false;

            if (type.IsValueType)
                return true;

            if (space == "UnityEngine" || space.StartsWith("UnityEngine.", StringComparison.Ordinal))
                return false;

            return plainOwner || type.IsDefined(typeof(SerializableAttribute), false);
        }

        private static Type ElementOf(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>) || type.GetGenericTypeDefinition() == typeof(HashSet<>)))
                return type.GetGenericArguments()[0];

            return null;
        }

        private static IReadOnlyList<SaveMember> Cached(Dictionary<Type, IReadOnlyList<SaveMember>> cache, Type type,
            Func<Type, IReadOnlyList<SaveMember>> find)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!cache.TryGetValue(type, out IReadOnlyList<SaveMember> members))
            {
                members = find(type);
                cache.Add(type, members);
            }

            return members;
        }
    }
}
