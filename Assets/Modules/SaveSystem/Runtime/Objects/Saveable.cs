using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Marks an object to save: its id, its prefab and which components of it and of its children (down to the next
    /// Saveable, which saves itself) are kept. Everything that has a serializer is saved unless unchecked in the
    /// Inspector, so a component added later is saved too. Any other component — a script without [Save], a component
    /// of a pack, a Light — is saved once given a mode: All Fields (everything it can) or Selected (the members picked
    /// in the Inspector); the mode overrides the serializer of a component that has one. Scene objects get their id in
    /// the editor; objects made in play (Instantiate, copies of scene objects too) get a new one in Awake and count as
    /// spawned.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(32000)]
    public sealed class Saveable : MonoBehaviour
    {
        [SerializeField] private string _id;
        [SerializeField] private string _prefabId;
        [Tooltip("An explicit list: only the components in Included are saved. Off is the old way: everything that " +
                 "can be saved, except Excluded")]
        [SerializeField] private bool _explicit;
        [Tooltip("Keys of the saved components (explicit list)")]
        [SerializeField] private List<string> _included = new();
        [Tooltip("Keys of the components that are not saved; everything else that can be saved is (old way)")]
        [SerializeField] private List<string> _excluded = new();
        [Tooltip("Options of the serializers, as <component key>|<option>")]
        [SerializeField] private List<string> _options = new();
        [Tooltip("Components saved in the All Fields or Selected mode, with their picked members")]
        [SerializeField] private List<ComponentRule> _rules = new();

        public string Id => _id;
        public string PrefabId => _prefabId;

        /// <summary>Made in play, not loaded with its scene: saved as spawned, restored from its prefab.</summary>
        public bool IsSpawned { get; internal set; }

        /// <summary>
        /// Whole scene: made in play under another object made in play and not a part of that object's prefab (a rocket
        /// under the robot that fired it) — saved as an object of its own that names its parent.
        /// </summary>
        internal bool IsOwnSpawn { get; set; }

        internal bool IsRegistered { get; set; }
        internal bool HasStarted { get; private set; }

        /// <summary>
        /// The Saveable keeps an explicit list: only the components checked on it are saved. A Saveable added in the
        /// editor starts this way, with the object's own Transform, Rigidbody and scripts checked. Without it (objects
        /// from before the list) everything that has a serializer is saved unless unchecked.
        /// </summary>
        public bool IsExplicit => _explicit;

        /// <summary>The whole-scene scope stands in for this object's setup: it has no Saveable of its own in the scene file.</summary>
        internal SavePolicy Policy { get; private set; }

        /// <summary>How the object stood when its scene loaded (whole-scene objects of the scene).</summary>
        internal SceneObjectBaseline? Baseline { get; private set; }

        /// <summary>A whole-scene object under an Animator: its Transform is the animation's, not saved.</summary>
        internal bool IsAnimated { get; private set; }

        public bool IsSaved(string componentKey) =>
            _explicit ? _included.Contains(componentKey) : !_excluded.Contains(componentKey);

        /// <summary>Checks or unchecks a component by its key.</summary>
        public void SetSaved(string componentKey, bool saved)
        {
            if (_explicit)
            {
                if (!saved)
                    _included.Remove(componentKey);
                else if (!_included.Contains(componentKey))
                    _included.Add(componentKey);

                return;
            }

            if (saved)
                _excluded.Remove(componentKey);
            else if (!_excluded.Contains(componentKey))
                _excluded.Add(componentKey);
        }

        /// <summary>
        /// Switches to an explicit list holding these components. Without keys the list is what a Saveable added in
        /// the editor starts with — the object's own Transform, Rigidbody and scripts (a script without a serializer
        /// or [Save] members gets the All Fields mode) — plus every saved component that was given a mode by hand.
        /// </summary>
        public void UseExplicitList(IEnumerable<string> componentKeys = null)
        {
            List<string> keys = componentKeys != null ? componentKeys.ToList() : DefaultKeys();
            _explicit = true;
            _excluded.Clear();
            _included = keys.Where(key => !string.IsNullOrEmpty(key)).Distinct().ToList();
        }

        private List<string> DefaultKeys()
        {
            var keys = new List<string>();
            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            foreach (KeyedComponent entry in ComponentKeys.Collect(this))
            {
                if (RuleOf(entry.Key) != null)
                {
                    // Set up by hand: stays as it is
                    if (IsSaved(entry.Key))
                        keys.Add(entry.Key);

                    continue;
                }

                if (entry.Path.Length > 0)
                    continue; // children are checked by hand

                Type type = entry.Component.GetType();
                switch (SavePolicy.CategoryOf(entry.Component, catalog))
                {
                    case SaveCoverage.Transforms:
                        keys.Add(entry.Key);
                        break;
                    case SaveCoverage.Physics:
                        // The body, not "collider enabled": that is rarely state
                        if (catalog.For(type)?.ComponentType.Name.StartsWith("Rigidbody", StringComparison.Ordinal) == true)
                            keys.Add(entry.Key);
                        break;
                    case SaveCoverage.Scripts:
                        if (catalog.For(type) != null)
                        {
                            keys.Add(entry.Key);
                        }
                        else if (SaveMembers.AllWithReferences(type).Count > 0)
                        {
                            _rules.Add(new ComponentRule { Key = entry.Key, Mode = SaveMode.AllFields });
                            keys.Add(entry.Key);
                        }

                        break;
                }
            }

            return keys;
        }

        public SaveMode ModeOf(string componentKey) => RuleOf(componentKey)?.Mode ?? SaveMode.Auto;

        public IReadOnlyList<string> MembersOf(string componentKey) =>
            (IReadOnlyList<string>)RuleOf(componentKey)?.Members ?? Array.Empty<string>();

        /// <summary>
        /// Saves the component in a mode: All Fields saves everything it can, Selected the members picked with
        /// <see cref="SetMembers"/>, Auto (the default) its serializer if it has one. Also checks it.
        /// </summary>
        public void SetMode(string componentKey, SaveMode mode)
        {
            ComponentRule rule = RuleOf(componentKey);
            if (mode == SaveMode.Auto)
            {
                if (rule != null)
                    _rules.Remove(rule);

                return;
            }

            if (rule == null)
            {
                rule = new ComponentRule { Key = componentKey };
                _rules.Add(rule);
            }

            rule.Mode = mode;
            SetSaved(componentKey, true);
        }

        /// <summary>Saves only these members of the component (fields or properties by name): the Selected mode.</summary>
        public void SetMembers(string componentKey, IEnumerable<string> members)
        {
            SetMode(componentKey, SaveMode.Selected);
            ComponentRule rule = RuleOf(componentKey);
            rule.Members = members?.Where(member => !string.IsNullOrEmpty(member)).Distinct().ToList() ?? new List<string>();
        }

        public bool IsOptionOn(string componentKey, string option) => _options.Contains(OptionEntry(componentKey, option));

        /// <summary>Turns a serializer option of a component on or off, such as live particles.</summary>
        public void SetOption(string componentKey, string option, bool on)
        {
            string entry = OptionEntry(componentKey, option);
            if (!on)
                _options.Remove(entry);
            else if (!_options.Contains(entry))
                _options.Add(entry);
        }

        /// <summary>This object's own state (active flags and saved components): for tests and debugging; scenes save it on their own.</summary>
        public JToken CaptureState() => SaveJson.ToToken(ObjectState.Capture(this, ComponentSerializerCatalog.Default));

        public void RestoreState(JToken state)
        {
            ObjectData data = SaveJson.FromToken<ObjectData>(state);
            if (data == null)
                return;

            ObjectState.RestoreActive(this, data);
            ObjectState.RestoreComponents(this, data, ComponentSerializerCatalog.Default);
        }

        internal ICollection<string> OptionsOf(string componentKey)
        {
            var options = new HashSet<string>();
            string prefix = componentKey + "|";
            foreach (string entry in _options)
            {
                if (entry.StartsWith(prefix, StringComparison.Ordinal))
                    options.Add(entry.Substring(prefix.Length));
            }

            return options;
        }

        private static string OptionEntry(string componentKey, string option) => componentKey + "|" + option;

        private ComponentRule RuleOf(string componentKey) => _rules.FirstOrDefault(rule => rule != null && rule.Key == componentKey);

        internal void SetId(string id) => _id = id;

        // ---------- Whole scene: a Saveable the scope adds in play; nothing of it is in the scene file ----------

        [SerializeField, HideInInspector] private bool _adopted;

        private static string _adoptId;
        private static string _adoptPrefabId;
        private static SavePolicy _adoptPolicy;
        private static SceneObjectBaseline? _adoptBaseline;

        /// <summary>
        /// Gives an object without a Saveable a hidden one that saves it by the policy of its scene's save system: an
        /// object of the scene (with its baseline) or one made in play (with the prefab it is a copy of).
        /// </summary>
        internal static Saveable Adopt(GameObject target, string id, SavePolicy policy, SceneObjectBaseline? baseline,
            string prefabId = null)
        {
            _adoptId = id;
            _adoptPrefabId = prefabId;
            _adoptPolicy = policy;
            _adoptBaseline = baseline;
            Saveable saveable;
            try
            {
                saveable = target.AddComponent<Saveable>(); // an active object runs Awake here
            }
            finally
            {
                _adoptId = null;
                _adoptPrefabId = null;
                _adoptPolicy = null;
                _adoptBaseline = null;
            }

            // An inactive object gets no Awake: the scan of its scene registers it
            if (saveable.Policy == null)
                saveable.TakeAdoption(id, prefabId, policy, baseline);

            saveable.hideFlags |= HideFlags.HideInInspector;
            return saveable;
        }

        private void TakeAdoption(string id, string prefabId, SavePolicy policy, SceneObjectBaseline? baseline)
        {
            _id = id;
            _prefabId = prefabId ?? string.Empty;
            _explicit = true;
            _adopted = true;
            Policy = policy;
            Baseline = baseline;
#if SAVE_ANIMATION
            Transform parent = transform.parent;
            IsAnimated = baseline.HasValue && parent != null && parent.GetComponentInParent<Animator>(true) != null;
#endif
        }

        /// <summary>Editor tests switch it off: a Saveable added there keeps the old "everything unless unchecked".</summary>
        internal static bool DefaultsOnAdd = true;

#if UNITY_EDITOR
        /// <summary>Set by the editor: gives scene objects an id and keeps prefab assets without one.</summary>
        internal static Action<Saveable> EditorValidate;

        /// <summary>
        /// The editor adds to it (ids, the project's settings): runs right when the component is added, where changing
        /// data is allowed. Add with +=: more than one part of the editor listens.
        /// </summary>
        internal static Action<Saveable> EditorAdded;

        private void OnValidate() => EditorValidate?.Invoke(this);

        private void Reset()
        {
            // Added by hand: an explicit list that starts with the object's own Transform, Rigidbody and scripts
            if (DefaultsOnAdd)
                UseExplicitList();

            EditorAdded?.Invoke(this);
        }
#endif

        private void Awake()
        {
            if (_adoptPolicy != null)
            {
                TakeAdoption(_adoptId, _adoptPrefabId, _adoptPolicy, _adoptBaseline);
            }
            else if (_adopted && Policy == null)
            {
                // A copy of a whole-scene object made in play (Instantiate): the hidden Saveable came along without its
                // policy. The copy is an object made in play like any other. With its policy it is the scene's own
                // object that was switched off when it was adopted (a pause menu) and is turned on only now
                _adopted = false;
                Destroy(this);
                return;
            }

            SaveRegistry.Register(this);
        }

        private void Start()
        {
            HasStarted = true;
            SaveRegistry.NotifyReady(this, started: true);
        }

        // Switched off before its Start: the Start never comes, so its data must not wait for it
        private void OnDisable()
        {
            if (!HasStarted)
                SaveRegistry.NotifyReady(this, started: false);
        }

        private void OnDestroy() => SaveRegistry.Unregister(this);
    }
}
