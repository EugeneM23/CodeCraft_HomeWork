using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Saves chosen members of any type as "key → value": the All Fields and Selected modes of a Saveable and
    /// registered objects (<see cref="ObjectSerializer"/>). Best effort, member by member: one that cannot be read or
    /// written is skipped with a warning and the rest go on — the right trade for components the game does not own.
    /// Values of a Unity object follow Unity's field rules, values of a plain object those of SaveJson.
    /// </summary>
    internal sealed class MembersSerializer : IComponentSerializer
    {
        private static readonly Dictionary<Type, MembersSerializer> AllCache = new();
        private static readonly Dictionary<Type, MembersSerializer> AllWithPrivateCache = new();
        private static readonly Dictionary<string, MembersSerializer> SelectedCache = new();
        private static readonly HashSet<string> CaptureWarned = new();

        private readonly IReadOnlyList<SaveMember> _members;
        private readonly JsonSerializer _json;
        private readonly bool _softReferences;

        /// <param name="softReferences">
        /// Members that hold Unity objects were not picked by hand (All Fields): one the save cannot express is left
        /// out without a warning instead of being written as unresolved.
        /// </param>
        public MembersSerializer(Type type, IReadOnlyList<SaveMember> members, bool softReferences = false)
        {
            ComponentType = type ?? throw new ArgumentNullException(nameof(type));
            _members = members ?? Array.Empty<SaveMember>();
            _json = typeof(UnityEngine.Object).IsAssignableFrom(type) ? ObjectsJson.Serializer : ObjectsJson.Components;
            _softReferences = softReferences;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWarnings() => CaptureWarned.Clear();

        public Type ComponentType { get; }
        public int Version => 1;
        public IReadOnlyList<SaveMember> Members => _members;

        /// <summary>The All Fields serializer of a type (<see cref="SaveMembers.AllWithReferences"/>).</summary>
        public static MembersSerializer AllFor(Type type)
        {
            if (!AllCache.TryGetValue(type, out MembersSerializer serializer))
            {
                serializer = new MembersSerializer(type, SaveMembers.AllWithReferences(type), softReferences: true);
                AllCache.Add(type, serializer);
            }

            return serializer;
        }

        /// <summary>All Fields with the private simple fields of a script too (<see cref="SaveMembers.AllWithPrivate"/>).</summary>
        public static MembersSerializer AllWithPrivateFor(Type type)
        {
            if (!AllWithPrivateCache.TryGetValue(type, out MembersSerializer serializer))
            {
                serializer = new MembersSerializer(type, SaveMembers.AllWithPrivate(type), softReferences: true);
                AllWithPrivateCache.Add(type, serializer);
            }

            return serializer;
        }

        private static readonly Dictionary<Type, MembersSerializer> StateCache = new();

        /// <summary>The serializer of a plain object given by an ISaveStateProvider (<see cref="SaveMembers.State"/>).</summary>
        public static MembersSerializer StateFor(Type type)
        {
            IReadOnlyList<SaveMember> members = SaveMembers.State(type);
            // The member lists are looked at again when the third-party list changes: a stale serializer is replaced
            if (!StateCache.TryGetValue(type, out MembersSerializer serializer) || !ReferenceEquals(serializer.Members, members))
            {
                serializer = new MembersSerializer(type, members);
                StateCache[type] = serializer;
            }

            return serializer;
        }

        /// <summary>The Selected serializer: the candidates of the type with these names, unknown names ignored.</summary>
        public static MembersSerializer SelectedFor(Type type, IEnumerable<string> names)
        {
            var wanted = new HashSet<string>(names ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            string key = type.AssemblyQualifiedName + "|" + string.Join("|", wanted.OrderBy(name => name, StringComparer.Ordinal));
            if (!SelectedCache.TryGetValue(key, out MembersSerializer serializer))
            {
                serializer = new MembersSerializer(type,
                    SaveMembers.Candidates(type).Where(member => wanted.Contains(member.Name)).ToList());
                SelectedCache.Add(key, serializer);
            }

            return serializer;
        }

        /// <summary>
        /// Selected in a scene saved whole: everything the scope takes from the script (all its fields, the private
        /// ones too if the scope says so) plus the members picked by hand — an object set up by hand is never saved
        /// worse than a plain one next to it.
        /// </summary>
        public static MembersSerializer SelectedOnTopOfAllFor(Type type, IEnumerable<string> names, bool privateFields)
        {
            var wanted = new HashSet<string>(names ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            string key = (privateFields ? "all+private|" : "all|") + type.AssemblyQualifiedName + "|"
                         + string.Join("|", wanted.OrderBy(name => name, StringComparer.Ordinal));
            if (!SelectedCache.TryGetValue(key, out MembersSerializer serializer))
            {
                var members = new List<SaveMember>(privateFields ? SaveMembers.AllWithPrivate(type) : SaveMembers.AllWithReferences(type));
                var taken = new HashSet<string>(members.Select(member => member.Name), StringComparer.Ordinal);
                members.AddRange(SaveMembers.Candidates(type).Where(member => wanted.Contains(member.Name) && taken.Add(member.Name)));
                serializer = new MembersSerializer(type, members, softReferences: true);
                SelectedCache.Add(key, serializer);
            }

            return serializer;
        }

        public JToken Capture(Component component) => CaptureObject(component);

        public void Restore(Component component, JToken data) => RestoreObject(component, data);

        public JToken Upgrade(JToken data, int fromVersion) =>
            throw new SaveMigrationException(ComponentType.Name, fromVersion);

        public JObject CaptureObject(object target)
        {
            var json = new JObject();
            foreach (SaveMember member in _members)
            {
                try
                {
                    object value = member.GetValue(target);
                    if (value == null)
                    {
                        json[member.Key] = JValue.CreateNull();
                    }
                    else if (member.IsMoment)
                    {
                        json[member.AgoKey] = ToAgo(Convert.ToDouble(value));
                    }
                    else if (!member.IsReference)
                    {
                        json[member.Key] = JToken.FromObject(value, _json);
                    }
                    else if (_softReferences)
                    {
                        // Not picked by hand: saved when everything in it can be expressed, otherwise not at all
                        JToken token;
                        using (SaveReferences.Quiet())
                            token = JToken.FromObject(value, _json);

                        if (!SaveReferences.ContainsUnresolved(token))
                            json[member.Key] = token;
                    }
                    else
                    {
                        JToken token = JToken.FromObject(value, _json);
                        json[member.Key] = SaveReferences.ContainsUnresolved(token) ? SaveReferences.UnresolvedToken() : token;
                    }
                }
                catch (Exception exception)
                {
                    if (CaptureWarned.Add(ComponentType.FullName + "." + member.Name))
                        Debug.LogWarning($"{ComponentType.Name}.{member.Name} could not be saved and is skipped: " +
                                         Unwrap(exception).Message);
                }
            }

            return json;
        }

        public void RestoreObject(object target, JToken data)
        {
            if (data is not JObject json)
                throw new FormatException($"the saved members of {ComponentType.Name} are not a JSON object");

            foreach (SaveMember member in _members)
            {
                // A moment comes back against the clock of this load; a save from before moments has it as a plain value
                if (member.IsMoment && json.TryGetValue(member.AgoKey, out JToken ago))
                {
                    try
                    {
                        member.SetValue(target, Convert.ChangeType(FromAgo(ago), member.Type));
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"{ComponentType.Name}.{member.Name} could not be loaded and keeps its value: " +
                                         Unwrap(exception).Message);
                    }

                    continue;
                }

                // Missing, or it holds a reference the save could not express: the member keeps what the scene gave it
                if (!json.TryGetValue(member.Key, out JToken token)
                    || (member.IsReference && SaveReferences.ContainsUnresolved(token)))
                    continue;

                try
                {
                    int notFound = SaveReferences.NotFound;
                    object value = token.Type == JTokenType.Null
                        ? member.Type.IsValueType ? Activator.CreateInstance(member.Type) : null
                        : token.ToObject(member.Type, _json);
                    // A list that lost one of its references on the way back is not what the game saved
                    if (member.IsReference && SaveReferences.HasHole(member, notFound, ComponentType.Name))
                        continue;

                    member.SetValue(target, value);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"{ComponentType.Name}.{member.Name} could not be loaded and keeps its value: " +
                                     Unwrap(exception).Message);
                }
            }
        }

        // ---------- Moments: "how long ago" instead of a reading of the clock ----------

        private const string NEVER = "never";       // -Infinity: it has not happened yet
        private const string NOT_YET = "not yet";   // +Infinity

        private static JToken ToAgo(double moment)
        {
            if (double.IsNegativeInfinity(moment))
                return NEVER;

            if (double.IsPositiveInfinity(moment) || double.IsNaN(moment))
                return NOT_YET;

            return SaveClock.Now - moment;
        }

        private static double FromAgo(JToken ago)
        {
            if (ago.Type == JTokenType.String)
                return (string)ago == NEVER ? double.NegativeInfinity : double.PositiveInfinity;

            return SaveClock.Now - (double)ago;
        }

        private static Exception Unwrap(Exception exception) =>
            exception is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : exception;
    }
}

namespace Saving.Objects
{
    /// <summary>
    /// The clock moments are counted against: Time.time as scripts read it in Update. The scripts of a scene that has
    /// just loaded get their Start inside the fixed step of the frame, where Time.time shows the time of the physics
    /// step — up to a whole long frame behind; a moment put back against that would be off by as much. So the time
    /// of the frame is noted when a scene arrives (outside the fixed step) and used for the rest of that frame.
    /// </summary>
    internal static class SaveClock
    {
        private static int _frame = -1;
        private static double _time;

        /// <summary>Called outside the fixed step: remembers the time of this frame.</summary>
        public static void Mark()
        {
            if (Time.inFixedTimeStep)
                return;

            _frame = Time.frameCount;
            _time = Time.timeAsDouble;
        }

        public static double Now => Time.inFixedTimeStep && _frame == Time.frameCount ? _time : Time.timeAsDouble;
    }
}
