using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Saving.Objects
{
    /// <summary>
    /// What the whole-scene scope takes from an object that has no Saveable of its own: by kind of component
    /// (<see cref="SaveCoverage"/>), the same for every object of the scene. A Transform is written only when the
    /// object moved since the scene loaded and is not driven by an Animator above it; a script is saved by its
    /// serializer, its [Save] members or all its fields. Also knows the prefabs the scene can spawn, by name.
    /// </summary>
    internal sealed class SavePolicy
    {
        private const string CLONE = "(Clone)";

        private readonly Dictionary<string, ScenePrefab> _prefabsByName = new(StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguous = new(StringComparer.Ordinal);

        public SavePolicy(SaveCoverage coverage, IEnumerable<ScenePrefab> prefabs = null, bool privateFields = false)
        {
            Coverage = coverage;
            PrivateFields = privateFields;
            if (prefabs == null)
                return;

            foreach (ScenePrefab entry in prefabs)
            {
                if (entry?.Prefab == null || string.IsNullOrEmpty(entry.Id))
                    continue;

                string name = entry.Prefab.name;
                if (_ambiguous.Contains(name))
                    continue;

                if (_prefabsByName.TryGetValue(name, out ScenePrefab other) && other.Prefab != entry.Prefab)
                {
                    // Two prefabs with one name: a copy made in play cannot tell which it is
                    _prefabsByName.Remove(name);
                    _ambiguous.Add(name);
                    continue;
                }

                _prefabsByName[name] = entry;
            }
        }

        public SaveCoverage Coverage { get; }

        /// <summary>A script saved by all its fields gives its private simple fields too.</summary>
        public bool PrivateFields { get; }

        /// <summary>All the fields of a script that has neither a serializer nor [Save] members; null when it has none to save.</summary>
        private IComponentSerializer AllFieldsOf(Type type)
        {
            if (PrivateFields)
                return SaveMembers.AllWithPrivate(type).Count > 0 ? MembersSerializer.AllWithPrivateFor(type) : null;

            return SaveMembers.AllWithReferences(type).Count > 0 ? MembersSerializer.AllFor(type) : null;
        }

        // Colliders of scene objects, on or off, as their scene loaded
        private readonly Dictionary<Component, bool> _enabledAtStart = new();

        /// <summary>Remembers how the colliders of a scene object stand: one that stays that way is not written.</summary>
        public void RememberStart(GameObject target, ComponentSerializerCatalog catalog)
        {
            foreach (Component component in target.GetComponents<Component>())
            {
                if (CategoryOf(component, catalog) == SaveCoverage.Physics && IsCollider(component, catalog)
                    && ObjectState.EnabledOf(component) is { } enabled)
                    _enabledAtStart[component] = enabled;
            }
        }

        private static bool IsCollider(Component component, ComponentSerializerCatalog catalog) =>
            catalog.For(component.GetType()).ComponentType.Name.StartsWith("Collider", StringComparison.Ordinal);

        /// <summary>Two prefabs of the scene share this name: objects made from them in play cannot be told apart.</summary>
        public bool IsAmbiguous(string objectName) => _ambiguous.Contains(BaseName(objectName));

        /// <summary>The prefab an object made in play is a copy of, by its name without "(Clone)".</summary>
        public bool TryFindPrefab(string objectName, out ScenePrefab prefab) =>
            _prefabsByName.TryGetValue(BaseName(objectName), out prefab);

        public static string BaseName(string objectName)
        {
            string name = objectName ?? string.Empty;
            while (name.EndsWith(CLONE, StringComparison.Ordinal))
                name = name.Substring(0, name.Length - CLONE.Length).TrimEnd();

            return name;
        }

        /// <summary>
        /// Who saves a component of a whole-scene object; null when it is not saved. On a load every saved Transform
        /// is applied, whatever the object looks like now.
        /// </summary>
        public IComponentSerializer SerializerFor(Saveable saveable, Component component, ComponentSerializerCatalog catalog,
            bool restoring)
        {
            SaveCoverage category = CategoryOf(component, catalog);
            if (category == SaveCoverage.None || (Coverage & category) == 0)
                return null;

            // An object of the scene answers for itself only: its children are objects of the scene too, and those
            // left out of the list (static ones) are left out on purpose. An object made in play brings its parts
            bool own = component.gameObject == saveable.gameObject;
            if (!own && saveable.Baseline.HasValue)
                return null;

            Type type = component.GetType();
            switch (category)
            {
                case SaveCoverage.Transforms:
                    // The parts of an object made in play stand as its prefab has them
                    if (!own)
                        return null;

                    if (!restoring && saveable.Baseline is { } baseline
                                   && (saveable.IsAnimated || baseline.SameTransform((Transform)component)))
                        return null;

                    return catalog.For(type);
                case SaveCoverage.Physics:
                    // "Collider on or off" as the scene has it: nothing to write
                    if (!restoring && _enabledAtStart.TryGetValue(component, out bool enabled)
                                   && ObjectState.EnabledOf(component) == enabled)
                        return null;

                    return catalog.For(type);
                case SaveCoverage.Scripts:
                    return catalog.For(type) ?? AllFieldsOf(type);
                default:
                    return catalog.For(type);
            }
        }

        /// <summary>
        /// Who saves a component of an object set up by hand (a Saveable with an explicit list) that its list leaves
        /// out, anywhere under it: scripts, bodies and effects as the scope's kinds say. Transforms and "collider on
        /// or off" stay with the list — the scope writes those against the scene's baseline, which only the objects it
        /// adopted have.
        /// </summary>
        public IComponentSerializer SerializerForUnlisted(Component component, ComponentSerializerCatalog catalog)
        {
            SaveCoverage category = CategoryOf(component, catalog);
            if (category == SaveCoverage.None || category == SaveCoverage.Transforms || (Coverage & category) == 0)
                return null;

            if (category == SaveCoverage.Physics && IsCollider(component, catalog))
                return null;

            Type type = component.GetType();
            IComponentSerializer serializer = catalog.For(type);
            if (serializer == null && category == SaveCoverage.Scripts)
                serializer = AllFieldsOf(type);

            return serializer;
        }

        /// <summary>A script of the game the scope saves by its fields (it has no serializer of its own).</summary>
        public bool TakesScript(Component component, ComponentSerializerCatalog catalog) =>
            (Coverage & SaveCoverage.Scripts) != 0 && CategoryOf(component, catalog) == SaveCoverage.Scripts
            && catalog.For(component.GetType()) == null;

        /// <summary>The kind a component falls under; None for what the scope never saves (renderers, UI, cameras…).</summary>
        public static SaveCoverage CategoryOf(Component component, ComponentSerializerCatalog catalog)
        {
            if (component == null || component is Saveable)
                return SaveCoverage.None;

            if (component is Transform)
                return SaveCoverage.Transforms;

            Type type = component.GetType();
            switch (catalog.KindOf(type))
            {
                case SerializerKind.BuiltIn:
                    string name = catalog.For(type).ComponentType.Name;
                    return name.StartsWith("Rigidbody", StringComparison.Ordinal) || name.StartsWith("Collider", StringComparison.Ordinal)
                        ? SaveCoverage.Physics
                        : SaveCoverage.Effects;
                case SerializerKind.Own:
                    return SaveCoverage.Scripts; // the game wrote a serializer for it: it wants it saved
            }

            return component is MonoBehaviour && !SaveMembers.UsesProperties(type) ? SaveCoverage.Scripts : SaveCoverage.None;
        }

        /// <summary>The object carries state worth bringing back: a script of the game or a body somewhere in it.</summary>
        public static bool HasState(GameObject target, ComponentSerializerCatalog catalog)
        {
            foreach (Component component in target.GetComponentsInChildren<Component>(true))
            {
                switch (CategoryOf(component, catalog))
                {
                    case SaveCoverage.Scripts:
                        return true;
                    case SaveCoverage.Physics
                        when catalog.For(component.GetType()).ComponentType.Name.StartsWith("Rigidbody", StringComparison.Ordinal):
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The object is an effect: it has no state of the game, only particle systems or sounds (an explosion, sparks
        /// of a hit). Made in play it is saved while it lasts, so a load shows it where it was.
        /// </summary>
        public static bool IsEffect(GameObject target, ComponentSerializerCatalog catalog)
        {
            foreach (Component component in target.GetComponentsInChildren<Component>(true))
            {
                if (CategoryOf(component, catalog) != SaveCoverage.Effects)
                    continue;

                string name = catalog.For(component.GetType()).ComponentType.Name;
                if (name == "ParticleSystem" || name == "AudioSource")
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// The whole-scene scope in play. When a scene with a save system set to Whole Scene has loaded, every object of
    /// the system's list that has no Saveable above it gets a hidden one (<see cref="Saveable.Adopt"/>) with the id
    /// from the list — from then on the registry, references, destroyed objects and the load phases treat it like any
    /// Saveable. Objects made in play are found at save time and saved as copies of the prefab they are named after.
    /// Nothing of this is written into the scene file.
    /// </summary>
    internal static class WholeScene
    {
        private sealed class SceneState
        {
            public SavePolicy Policy;
            public readonly HashSet<GameObject> Originals = new();
            public readonly HashSet<GameObject> Ignored = new();
        }

        private static readonly Dictionary<Scene, SceneState> Scenes = new();
        private static readonly Dictionary<string, GameObject> Prefabs = new(StringComparer.Ordinal);
        private static readonly HashSet<string> Warned = new(StringComparer.Ordinal);
        private static readonly List<Saveable> Buffer = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Scenes.Clear();
            Prefabs.Clear();
            Warned.Clear();
        }

        public static SavePolicy PolicyOf(Scene scene) => Scenes.TryGetValue(scene, out SceneState state) ? state.Policy : null;

        /// <summary>
        /// Who saves a component that the explicit list of a Saveable set up by hand leaves out, in a scene saved
        /// whole (<see cref="SavePolicy.SerializerForUnlisted"/>). Under an object of the scene only what the scene
        /// itself has there counts: a muzzle flash made in play under an enemy is not its state and would not be
        /// there after a load anyway. An object made in play brings all its parts.
        /// </summary>
        public static IComponentSerializer UnlistedSerializerFor(Saveable saveable, Component component, ComponentSerializerCatalog catalog)
        {
            if (!Scenes.TryGetValue(saveable.gameObject.scene, out SceneState state))
                return null;

            if (!saveable.IsSpawned && !state.Originals.Contains(component.gameObject))
                return null;

            return state.Policy.SerializerForUnlisted(component, catalog);
        }

        /// <summary>A prefab some whole-scene scene of this session can spawn, by its id.</summary>
        public static bool TryGetPrefab(string id, out GameObject prefab)
        {
            prefab = null;
            return !string.IsNullOrEmpty(id) && Prefabs.TryGetValue(id, out prefab) && prefab != null;
        }

        /// <summary>The registry calls it when a scene has loaded, right before it scans the scene for Saveables.</summary>
        public static void Adopt(Scene scene)
        {
            Scenes.Remove(scene);
            // The system of the scene, wherever it is now: the first one moves itself to DontDestroyOnLoad in Awake
            SaveLoadSystem system = SaveLoadSystem.OfScene(scene);
            if (system == null || system.Scope != SaveScope.WholeScene)
                return;

            var state = new SceneState { Policy = new SavePolicy(system.Coverage, system.ScenePrefabs, system.PrivateFields) };
            Scenes[scene] = state;
            foreach (ScenePrefab entry in system.ScenePrefabs)
            {
                if (entry?.Prefab != null && !string.IsNullOrEmpty(entry.Id))
                    Prefabs[entry.Id] = entry.Prefab;
            }

            if (system.SceneObjects.Count == 0 && Warned.Add("no-list/" + scene.path))
                Debug.LogWarning($"The save system of '{scene.name}' is set to Whole Scene but has no list of the scene's " +
                                 "objects: save the scene in the editor once (the list is filled as the scene is saved).",
                    system);

            // Objects under a Saveable set up by hand are that Saveable's business
            var owned = new HashSet<Transform>();
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject root in roots)
            {
                root.GetComponentsInChildren(true, Buffer);
                foreach (Saveable saveable in Buffer)
                    owned.Add(saveable.transform);
            }

            Buffer.Clear();
            ComponentSerializerCatalog catalog = ComponentSerializerCatalog.Default;
            IReadOnlyList<SceneObjectId> listed = system.SceneObjects;
            IReadOnlyList<SceneObjectBaseline> baselines = system.Baselines;
            for (int index = 0; index < listed.Count; index++)
            {
                GameObject target = listed[index]?.Object;
                string id = listed[index]?.Id;
                // An id that is alive already: the object of an earlier load of this scene that outlived it
                // (DontDestroyOnLoad) — the newcomer is the copy
                if (target == null || string.IsNullOrEmpty(id) || IsUnder(target.transform, owned) || SaveRegistry.TryGet(id, out _))
                    continue;

                SceneObjectBaseline baseline = index < baselines.Count ? baselines[index] : new SceneObjectBaseline(target);
                Saveable.Adopt(target, id, state.Policy, baseline);
                state.Policy.RememberStart(target, catalog);
            }

            // The scene's own objects are those it had when its save system woke — before any other script of the scene.
            // What a DI context or a bootstrap made in Awake since then (pools, a player) was made in play, like
            // everything that appears later
            IReadOnlyCollection<GameObject> atAwake = system.ObjectsAtAwake;
            if (atAwake != null)
            {
                foreach (GameObject original in atAwake)
                {
                    if (original != null)
                        state.Originals.Add(original);
                }

                return;
            }

            // A system that has not woken in this scene (added by code later): everything here now is the scene's own
            foreach (GameObject root in roots)
            {
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    state.Originals.Add(node.gameObject);
            }
        }

        /// <summary>
        /// The prefab an object made in play is a copy of: one the object's scene uses, or — for an object spawned
        /// into another scene than the one that names its prefab (a spawner in the level, pools in a systems scene) —
        /// one any other whole-scene scene in play uses.
        /// </summary>
        private static bool TryFindPrefab(SceneState state, string objectName, out ScenePrefab prefab)
        {
            if (state.Policy.TryFindPrefab(objectName, out prefab))
                return true;

            if (state.Policy.IsAmbiguous(objectName))
                return false;

            foreach (KeyValuePair<Scene, SceneState> pair in Scenes)
            {
                if (pair.Value != state && pair.Key.isLoaded && pair.Value.Policy.TryFindPrefab(objectName, out prefab))
                    return true;
            }

            return false;
        }

        /// <summary>The object itself carries state: a script of the game or a body on it, not somewhere below.</summary>
        private static bool HasOwnState(GameObject target, ComponentSerializerCatalog catalog)
        {
            foreach (Component component in target.GetComponents<Component>())
            {
                switch (SavePolicy.CategoryOf(component, catalog))
                {
                    case SaveCoverage.Scripts:
                        return true;
                    case SaveCoverage.Physics
                        when catalog.For(component.GetType()).ComponentType.Name.StartsWith("Rigidbody", StringComparison.Ordinal):
                        return true;
                }
            }

            return false;
        }

        public static void Forget(Scene scene) => Scenes.Remove(scene);

        /// <summary>
        /// Before a save: objects made in play in whole-scene scenes that carry state get a hidden Saveable that saves
        /// them as a copy of the prefab they are named after. One-shot effects (no script, no body) are left alone.
        /// </summary>
        public static void AdoptMadeInPlay(ComponentSerializerCatalog catalog)
        {
            foreach (KeyValuePair<Scene, SceneState> pair in Scenes)
            {
                if (!pair.Key.isLoaded)
                    continue;

                foreach (GameObject root in pair.Key.GetRootGameObjects())
                    Visit(root.transform, pair.Value, catalog);
            }
        }

        /// <summary>
        /// On a load: an object the game made again on its own (a weapon given in Start, a pool) that a saved copy of
        /// this prefab under this parent stands for — so the saved one is not made a second time.
        /// </summary>
        public static GameObject FindMadeInPlay(Scene scene, Transform parent, GameObject prefab)
        {
            if (!Scenes.TryGetValue(scene, out SceneState state) || prefab == null)
                return null;

            if (parent != null)
            {
                foreach (Transform child in parent)
                {
                    if (IsUnclaimedCopy(child.gameObject, prefab, state))
                        return child.gameObject;
                }

                return null;
            }

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (IsUnclaimedCopy(root, prefab, state))
                    return root;
            }

            return null;
        }

        private static bool IsUnclaimedCopy(GameObject candidate, GameObject prefab, SceneState state) =>
            candidate.activeInHierarchy && !state.Originals.Contains(candidate) && !candidate.TryGetComponent(out Saveable _)
            && SavePolicy.BaseName(candidate.name) == prefab.name;

        private static void Visit(Transform node, SceneState state, ComponentSerializerCatalog catalog)
        {
            GameObject target = node.gameObject;
            if (target.TryGetComponent(out Saveable saveable))
            {
                // An object made in play saves the hierarchy its prefab gave it; what the game put under it later
                // (a rocket under the robot that fired it, a weapon in a hand) is an object of its own
                if (saveable.IsSpawned)
                {
                    VisitAddedUnder(saveable, state, catalog);
                    return;
                }
            }
            else if (!state.Originals.Contains(target))
            {
                TryAdoptMadeInPlay(target, state, catalog);
                return;
            }

            for (int index = 0; index < node.childCount; index++)
                Visit(node.GetChild(index), state, catalog);
        }

        /// <summary>
        /// Under an object made in play: a child its prefab does not have was added by the game. One that is a copy of
        /// a prefab the scenes know and carries state is adopted as an object of its own (saved with its parent's id, so
        /// a load makes it again or takes the one the game made); effects and plain decoration stay the parent's.
        /// </summary>
        private static void VisitAddedUnder(Saveable owner, SceneState state, ComponentSerializerCatalog catalog)
        {
            // A prefab with a Saveable of its own answers for its hierarchy the way it always did
            if (owner.Policy == null || !TryGetPrefab(owner.PrefabId, out GameObject prefab))
                return;

            WalkAdded(owner.transform, prefab.transform, state, catalog);
        }

        private static void WalkAdded(Transform node, Transform prefabNode, SceneState state, ComponentSerializerCatalog catalog)
        {
            for (int index = 0; index < node.childCount; index++)
            {
                Transform child = node.GetChild(index);
                if (child.TryGetComponent(out Saveable saveable))
                {
                    if (saveable.IsOwnSpawn)
                        VisitAddedUnder(saveable, state, catalog);

                    continue;
                }

                Transform twin = TwinOf(child, node, prefabNode);
                if (twin != null)
                    WalkAdded(child, twin, state, catalog);
                else
                    TryAdoptMadeInPlay(child.gameObject, state, catalog, underSpawned: true);
            }
        }

        /// <summary>The child of the prefab this child came from: same name, same place among the children of that name.</summary>
        private static Transform TwinOf(Transform child, Transform parent, Transform prefabParent)
        {
            int place = 0;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform sibling = parent.GetChild(index);
                if (sibling == child)
                    break;

                if (sibling.name == child.name)
                    place++;
            }

            for (int index = 0; index < prefabParent.childCount; index++)
            {
                Transform candidate = prefabParent.GetChild(index);
                if (candidate.name == child.name && place-- == 0)
                    return candidate;
            }

            return null;
        }

        private static void TryAdoptMadeInPlay(GameObject target, SceneState state, ComponentSerializerCatalog catalog,
            bool underSpawned = false)
        {
            // Inactive ones wait (a pool): they are looked at again once they are in use
            if (!target.activeInHierarchy || state.Ignored.Contains(target))
                return;

            bool effect = false;
            if (!SavePolicy.HasState(target, catalog))
            {
                // No script, no body: an effect is saved while it lasts, anything else is not state at all
                effect = !underSpawned && (state.Policy.Coverage & SaveCoverage.Effects) != 0 && SavePolicy.IsEffect(target, catalog);
                if (!effect)
                {
                    // An empty folder (a pool that holds nothing in use yet) is looked at again: it fills up later
                    if (target.GetComponents<Component>().Length > 1)
                        state.Ignored.Add(target);

                    return;
                }

                // Over already and waiting for the game to destroy it: nothing to bring back
                if (!SavedEffect.IsAlive(target))
                    return;
            }

            if (!TryFindPrefab(state, target.name, out ScenePrefab prefab))
            {
                // A folder the game keeps its objects in (a pool, a "Bullets" root made by code): it has nothing of
                // its own to save, so what is under it is judged one by one — now and on every later save
                if (!effect && !HasOwnState(target, catalog))
                {
                    for (int index = 0; index < target.transform.childCount; index++)
                        Visit(target.transform.GetChild(index), state, catalog);

                    return;
                }

                state.Ignored.Add(target);
                string name = SavePolicy.BaseName(target.name);
                // An effect built by code (a sound made on the fly) is not worth a warning
                if (!effect && Warned.Add("no-prefab/" + name))
                    Debug.LogWarning(state.Policy.IsAmbiguous(target.name)
                        ? $"'{target.name}' was made in play, and the scene uses several prefabs named '{name}': it cannot " +
                          "be told which one it is a copy of, so it is not saved. Rename one, or put a Saveable on the prefab."
                        : $"'{target.name}' was made in play and the scene uses no prefab named '{name}': it is not saved. " +
                          "Put a Saveable on its prefab to save such objects.", target);
                return;
            }

            Saveable adopted = Saveable.Adopt(target, SaveRegistry.NewId(), state.Policy, null, prefab.Id);
            adopted.IsOwnSpawn = underSpawned;
            SaveRegistry.Rekey(adopted, adopted.Id); // counts as made in play
            // Adopted in this very pass: what the game has put under it since it was made is looked at right away
            VisitAddedUnder(adopted, state, catalog);
        }

        private static bool IsUnder(Transform node, HashSet<Transform> owners)
        {
            if (owners.Count == 0)
                return false;

            for (Transform current = node; current != null; current = current.parent)
            {
                if (owners.Contains(current))
                    return true;
            }

            return false;
        }

    }
}
