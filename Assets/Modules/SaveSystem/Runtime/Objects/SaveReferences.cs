using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Saving.Objects
{
    /// <summary>
    /// References to Unity objects inside saved data. A scene object is the nearest Saveable above it, the path from
    /// it and the component on that object; a prefab is its prefab id; an asset is its key in the asset table of the
    /// settings. Anything else cannot be expressed: it is saved as "unresolved" with a warning (once per object) and a
    /// load keeps what the scene has there; a reference not found on load is null with a warning. The objects layer
    /// sets <see cref="Settings"/>.
    /// </summary>
    internal static class SaveReferences
    {
        private static readonly HashSet<string> Warned = new();

        public static SaveSystemSettings Settings { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Warned.Clear();
            Settings = null;
            _quiet = 0;
        }

        public static JToken Write(Object target)
        {
            if (target == null)
                return JValue.CreateNull();

            GameObject go = target as GameObject;
            if (go == null && target is Component component)
                go = component.gameObject;

            if (go != null && go.scene.IsValid())
            {
                Saveable owner = go.GetComponentInParent<Saveable>(true);
                if (owner == null || string.IsNullOrEmpty(owner.Id))
                    return Unresolved(target, "is a scene object outside any Saveable");

                var json = new JObject { ["id"] = owner.Id };
                string path = ComponentKeys.PathOf(owner.transform, go.transform);
                if (path.Length > 0)
                    json["path"] = path;

                if (target is Component targetComponent)
                    json["component"] = NameOf(targetComponent);

                return json;
            }

            if (target is GameObject prefab && prefab.TryGetComponent(out Saveable root) && !string.IsNullOrEmpty(root.PrefabId)
                && Settings != null && Settings.TryGetPrefab(root.PrefabId, out _))
                return new JObject { ["prefab"] = root.PrefabId };

            if (Settings != null && Settings.TryGetAssetKey(target, out string key))
                return new JObject { ["asset"] = key };

            return Unresolved(target, "is not a Saveable, a prefab with a Saveable or an asset of the settings' folders");
        }

        public static Object Read(JToken token, Type type)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            Object found = null;
            if (token is JObject json)
            {
                if (json.TryGetValue("id", out JToken id))
                {
                    if (SaveRegistry.TryGet((string)id, out Saveable owner))
                    {
                        Transform node = ComponentKeys.FindChild(owner.transform, (string)json["path"]);
                        string name = (string)json["component"];
                        if (node != null)
                            found = name == null ? node.gameObject : FindComponent(node.gameObject, name);
                    }
                }
                else if (json.TryGetValue("prefab", out JToken prefabId))
                {
                    if (Settings != null && Settings.TryGetPrefab((string)prefabId, out GameObject prefab))
                        found = prefab;
                }
                else if (json.TryGetValue("asset", out JToken key))
                {
                    if (Settings != null && Settings.TryGetAsset((string)key, out Object asset))
                        found = asset;
                }
            }

            found = Fit(found, type);
            if (found == null)
            {
                NotFound++;
                Debug.LogWarning($"The saved reference {token.ToString(Formatting.None)} was not found; it is set to null.");
            }

            return found;
        }

        /// <summary>
        /// How many saved references were not found so far: a serializer compares it before and after reading a member
        /// to tell that the member came back with a hole (see <see cref="HasHole"/>).
        /// </summary>
        public static int NotFound { get; private set; }

        /// <summary>
        /// A member that holds several references (a list, a dictionary, a class) and lost one of them on the way
        /// back: the game does not expect a null in its list of actors, so such a member keeps what the scene has —
        /// with a warning that names it. A member that is one reference simply becomes null.
        /// </summary>
        public static bool HasHole(SaveMember member, int notFoundBefore, string owner)
        {
            if (NotFound == notFoundBefore || typeof(Object).IsAssignableFrom(member.Type))
                return false;

            Debug.LogWarning($"{owner}.{member.Name} holds a saved reference that was not found: it is not loaded and " +
                             "keeps what the scene has there.");
            return true;
        }

        /// <summary>The component's type name on its object; a second one of that type is "Type#1".</summary>
        private static string NameOf(Component component)
        {
            string type = component.GetType().Name;
            int index = 0;
            foreach (Component other in component.GetComponents<Component>())
            {
                if (other == component)
                    break;

                if (other != null && other.GetType().Name == type)
                    index++;
            }

            return index == 0 ? type : $"{type}#{index}";
        }

        private static Component FindComponent(GameObject go, string name)
        {
            int hash = name.LastIndexOf('#');
            string type = name;
            int wanted = 0;
            if (hash > 0 && int.TryParse(name.Substring(hash + 1), out int number))
            {
                type = name.Substring(0, hash);
                wanted = number;
            }

            int seen = 0;
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != type)
                    continue;

                if (seen == wanted)
                    return component;

                seen++;
            }

            return null;
        }

        /// <summary>A GameObject where a component is wanted gives its component, a component gives its GameObject.</summary>
        private static Object Fit(Object found, Type type)
        {
            if (found == null || type.IsInstanceOfType(found))
                return found;

            if (found is GameObject go && typeof(Component).IsAssignableFrom(type))
                return go.GetComponent(type);

            if (found is Component component && type == typeof(GameObject))
                return component.gameObject;

            return null;
        }

        /// <summary>
        /// A reference the save could not express: not null — the object held one — so a load leaves what the scene has
        /// there (a manager found in Start, a sound from the Inspector) instead of clearing it.
        /// </summary>
        public static bool IsUnresolved(JToken token) =>
            token is JObject { Count: 1 } json && json.TryGetValue(UNRESOLVED, out JToken flag)
            && flag.Type == JTokenType.Boolean && (bool)flag;

        /// <summary>
        /// The value holds a reference the save could not express anywhere inside (an element of a list, a field of a
        /// class): the whole member is then left to the scene — a list with a hole in it helps nobody.
        /// </summary>
        public static bool ContainsUnresolved(JToken token)
        {
            if (token is not JContainer container)
                return false;

            if (IsUnresolved(token))
                return true;

            foreach (JToken child in container.Children())
            {
                if (ContainsUnresolved(child))
                    return true;
            }

            return false;
        }

        /// <summary>What a member that holds such a reference is saved as.</summary>
        public static JToken UnresolvedToken() => new JObject { [UNRESOLVED] = true };

        /// <summary>
        /// No warnings inside: the All Fields mode looks at every reference of a script, wiring included, and quietly
        /// leaves out what the save cannot express.
        /// </summary>
        public static QuietScope Quiet(bool quiet = true)
        {
            if (quiet)
                _quiet++;

            return new QuietScope(quiet);
        }

        public readonly struct QuietScope : IDisposable
        {
            private readonly bool _active;

            public QuietScope(bool active) => _active = active;

            public void Dispose()
            {
                if (_active)
                    _quiet--;
            }
        }

        private const string UNRESOLVED = "unresolved";

        private static int _quiet;

        private static JToken Unresolved(Object target, string why)
        {
            if (_quiet == 0 && Warned.Add($"{target.GetType().Name}/{target.name}"))
                Debug.LogWarning($"'{target.name}' ({target.GetType().Name}) {why}: it is not saved, and a load keeps " +
                                 "what the scene has there.", target);

            return UnresolvedToken();
        }
    }
}
