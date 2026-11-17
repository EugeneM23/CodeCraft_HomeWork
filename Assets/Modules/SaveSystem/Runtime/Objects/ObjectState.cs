using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// One Saveable's state: its active flag, the flags of children that hold saved components and the data of
    /// every checked component that has a serializer. A failing component is logged and skipped, the rest go on;
    /// on save it keeps its previous entry.
    /// </summary>
    internal static class ObjectState
    {
        private static readonly HashSet<string> MissingWarned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWarnings() => MissingWarned.Clear();

        public static ObjectData Capture(Saveable saveable, ComponentSerializerCatalog catalog, ObjectData previous = null)
        {
            var data = new ObjectData { Active = saveable.gameObject.activeSelf };
            foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
            {
                IComponentSerializer serializer = SerializerFor(saveable, entry.Key, entry.Component, catalog, false, out bool byScope);
                if (serializer == null)
                    continue;

                if (entry.Path.Length > 0)
                    data.Children[entry.Path] = entry.Component.gameObject.activeSelf;

                try
                {
                    JToken captured;
                    // A whole-scene object: nobody picked its components by hand, so a reference the save cannot
                    // express (a sound of an AudioSource) is left to the scene without a warning
                    using (SaveReferences.Quiet(byScope))
                    using (SerializerOptions.Use(saveable.OptionsOf(entry.Key)))
                        captured = serializer.Capture(entry.Component);

                    data.Components[entry.Key] = new ComponentData
                    {
                        V = serializer.Version,
                        Enabled = EnabledOf(entry.Component),
                        Data = captured
                    };
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Saving {entry.Key} of '{saveable.name}' failed, its previous data is kept: " +
                                   exception.Message, saveable);
                    if (previous != null && previous.Components.TryGetValue(entry.Key, out ComponentData old))
                        data.Components[entry.Key] = old;
                }
            }

            CaptureState(saveable, data, previous);

            return data;
        }

        public static void RestoreActive(Saveable saveable, ObjectData data)
        {
            foreach (KeyValuePair<string, bool> pair in data.Children)
            {
                Transform child = ComponentKeys.FindChild(saveable.transform, pair.Key);
                if (child != null)
                    child.gameObject.SetActive(pair.Value);
            }

            saveable.gameObject.SetActive(data.Active);
        }

        public static void RestoreComponents(Saveable saveable, ObjectData data, ComponentSerializerCatalog catalog)
        {
            var byKey = new Dictionary<string, Component>();
            foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
                byKey[entry.Key] = entry.Component;

            List<string> missing = null;
            foreach (KeyValuePair<string, ComponentData> pair in data.Components)
            {
                if (!byKey.TryGetValue(pair.Key, out Component component))
                {
                    if (MissingWarned.Add(saveable.Id + "/" + pair.Key))
                        (missing ??= new List<string>()).Add(pair.Key);
                    continue;
                }

                IComponentSerializer serializer = SerializerFor(saveable, pair.Key, component, catalog, restoring: true);
                if (serializer == null)
                    continue;

                try
                {
                    using (SerializerOptions.Use(saveable.OptionsOf(pair.Key)))
                        serializer.Restore(component, Upgrade(serializer, pair.Value));
                    if (pair.Value.Enabled.HasValue)
                        SetEnabled(component, pair.Value.Enabled.Value);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Loading {pair.Key} of '{saveable.name}' failed, it is skipped: {exception.Message}",
                        saveable);
                }
            }

            // A whole-scene object: nobody picked these components by hand (a muzzle flash that was under the weapon
            // when the game was saved), so nobody is told
            if (saveable.Policy == null)
                WarnMissing(saveable, missing);

            RestoreState(saveable, data);
        }

        // ---------- Plain objects of the game (ISaveStateProvider) ----------

        private readonly struct StateEntry
        {
            public StateEntry(string key, object target)
            {
                Key = key;
                Target = target;
            }

            public string Key { get; }
            public object Target { get; }
        }

        /// <summary>
        /// What the providers among the object's components hand over, keyed "&lt;component key&gt;:&lt;class&gt;"; a second
        /// object of the same class from the same provider gets "#1", a third "#2".
        /// </summary>
        private static List<StateEntry> CollectState(Saveable saveable)
        {
            List<StateEntry> result = null;
            foreach (KeyedComponent entry in ComponentKeys.Collect(saveable))
            {
                if (entry.Component is not ISaveStateProvider provider)
                    continue;

                var given = new List<object>();
                try
                {
                    provider.CollectState(given);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, entry.Component);
                    continue;
                }

                Dictionary<string, int> seen = null;
                foreach (object target in given)
                {
                    if (target == null || target is UnityEngine.Object || MembersSerializer.StateFor(target.GetType()).Members.Count == 0)
                        continue;

                    string name = target.GetType().Name;
                    seen ??= new Dictionary<string, int>(StringComparer.Ordinal);
                    seen.TryGetValue(name, out int index);
                    seen[name] = index + 1;
                    string key = entry.Key + ":" + name + (index > 0 ? "#" + index : string.Empty);
                    (result ??= new List<StateEntry>()).Add(new StateEntry(key, target));
                }
            }

            return result;
        }

        private static void CaptureState(Saveable saveable, ObjectData data, ObjectData previous)
        {
            List<StateEntry> state = CollectState(saveable);
            if (state == null)
                return;

            foreach (StateEntry entry in state)
            {
                try
                {
                    (entry.Target as ISaveCallbacks)?.OnBeforeSave();
                    (data.State ??= new Dictionary<string, JToken>())[entry.Key] =
                        MembersSerializer.StateFor(entry.Target.GetType()).CaptureObject(entry.Target);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Saving {entry.Key} of '{saveable.name}' failed, its previous data is kept: {exception.Message}", saveable);
                    if (previous?.State != null && previous.State.TryGetValue(entry.Key, out JToken old))
                        (data.State ??= new Dictionary<string, JToken>())[entry.Key] = old;
                }
            }
        }

        private static void RestoreState(Saveable saveable, ObjectData data)
        {
            if (data.State == null || data.State.Count == 0)
                return;

            List<StateEntry> state = CollectState(saveable);
            if (state == null)
                return;

            List<ISaveCallbacks> restored = null;
            foreach (StateEntry entry in state)
            {
                if (!data.State.TryGetValue(entry.Key, out JToken saved))
                    continue;

                try
                {
                    MembersSerializer.StateFor(entry.Target.GetType()).RestoreObject(entry.Target, saved);
                    if (entry.Target is ISaveCallbacks callbacks)
                        (restored ??= new List<ISaveCallbacks>()).Add(callbacks);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Loading {entry.Key} of '{saveable.name}' failed, it is skipped: {exception.Message}", saveable);
                }
            }

            // Once all of them have their data: one may read another while it tells its views
            if (restored == null)
                return;

            foreach (ISaveCallbacks callbacks in restored)
            {
                try
                {
                    callbacks.OnAfterLoad();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, saveable);
                }
            }
        }

        /// <summary>
        /// One warning for an object, not one per component: a child made in play (a picked-up weapon) that is not
        /// there after the load brings dozens of saved components with it.
        /// </summary>
        private static void WarnMissing(Saveable saveable, List<string> missing)
        {
            if (missing == null)
                return;

            if (missing.Count == 1)
            {
                Debug.LogWarning($"'{saveable.name}' has no component {missing[0]} any more; its saved data is skipped.", saveable);
                return;
            }

            const int SHOWN = 3;
            string first = string.Join(", ", missing.GetRange(0, Math.Min(SHOWN, missing.Count)));
            string rest = missing.Count > SHOWN ? $" and {missing.Count - SHOWN} more" : string.Empty;
            Debug.LogWarning($"'{saveable.name}' has {missing.Count} saved components that are not on it any more ({first}{rest}); " +
                             "their saved data is skipped. Objects made in play come back only from prefabs with a Saveable.",
                saveable);
        }

        /// <summary>
        /// Who saves a component of the Saveable: nobody when unchecked; the mode's MembersSerializer for All Fields
        /// and Selected; otherwise its serializer from the catalog, if it has one. In a scene saved whole, what an
        /// explicit list leaves out is taken by the scope of the scene (<see cref="SavePolicy.SerializerForUnlisted"/>).
        /// </summary>
        public static IComponentSerializer SerializerFor(Saveable saveable, string key, Component component,
            ComponentSerializerCatalog catalog, bool restoring = false) =>
            SerializerFor(saveable, key, component, catalog, restoring, out _);

        /// <param name="byScope">The scope of the scene picked the component, not a list somebody set up by hand.</param>
        public static IComponentSerializer SerializerFor(Saveable saveable, string key, Component component,
            ComponentSerializerCatalog catalog, bool restoring, out bool byScope)
        {
            // A whole-scene object: the scope of its scene decides, the same for every object
            byScope = saveable.Policy != null;
            if (byScope)
                return saveable.Policy.SerializerFor(saveable, component, catalog, restoring);

            if (!saveable.IsSaved(key))
            {
                // An object set up by hand in a scene saved whole: its list says how its components are saved, the
                // scope takes the rest (the Animator and the effects of an enemy), so it is not saved worse than a
                // plain object next to it
                byScope = saveable.IsExplicit;
                return byScope
                    ? WholeScene.UnlistedSerializerFor(saveable, component, catalog)
                    : null;
            }

            // In a scene saved whole a script's mode adds to what the scope takes, it does not narrow it
            SavePolicy scope = WholeScene.PolicyOf(saveable.gameObject.scene);
            if (scope != null && scope.TakesScript(component, catalog))
            {
                switch (saveable.ModeOf(key))
                {
                    case SaveMode.AllFields:
                        byScope = true;
                        return scope.PrivateFields
                            ? MembersSerializer.AllWithPrivateFor(component.GetType())
                            : MembersSerializer.AllFor(component.GetType());
                    case SaveMode.Selected:
                        byScope = true;
                        return MembersSerializer.SelectedOnTopOfAllFor(component.GetType(), saveable.MembersOf(key), scope.PrivateFields);
                }
            }

            return saveable.ModeOf(key) switch
            {
                SaveMode.AllFields => MembersSerializer.AllFor(component.GetType()),
                SaveMode.Selected => MembersSerializer.SelectedFor(component.GetType(), saveable.MembersOf(key)),
                _ => catalog.For(component.GetType())
            };
        }

        private static JToken Upgrade(IComponentSerializer serializer, ComponentData entry)
        {
            if (entry.V > serializer.Version)
                throw new InvalidOperationException($"it was saved with version {entry.V}, newer than {serializer.Version}");

            if (entry.V < 1)
                throw new InvalidOperationException($"it has an invalid version {entry.V}");

            JToken data = entry.Data?.DeepClone();
            for (int version = entry.V; version < serializer.Version; version++)
                data = serializer.Upgrade(data, version);

            return data;
        }

        internal static bool? EnabledOf(Component component)
        {
            switch (component)
            {
                case Behaviour behaviour:
                    return behaviour.enabled;
                case Renderer renderer:
                    return renderer.enabled;
#if SAVE_PHYSICS
                case Collider collider:
                    return collider.enabled;
#endif
                default:
                    return null;
            }
        }

        private static void SetEnabled(Component component, bool enabled)
        {
            switch (component)
            {
                case Behaviour behaviour:
                    behaviour.enabled = enabled;
                    break;
                case Renderer renderer:
                    renderer.enabled = enabled;
                    break;
#if SAVE_PHYSICS
                case Collider collider:
                    collider.enabled = enabled;
                    break;
#endif
            }
        }
    }
}
