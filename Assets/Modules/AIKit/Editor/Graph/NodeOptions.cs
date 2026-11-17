using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using InternalEditorUtility = UnityEditorInternal.InternalEditorUtility;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// The values a text setting of a node is picked from (step 90, C5; handbook §8.1, §32.2: a key is picked from a list,
    /// never typed) — taken from the project, not from the character the window was opened for: the moves of every
    /// weapon, the weapons, the actions (the animator states tagged Action of every controller, the actions of moves,
    /// dashes, drawing and sheathing, the abilities), the groups of attack places, the tags, the types. Read once and
    /// kept until the project changes. Only the editor reads the animators: the model knows nothing but names (C22).
    /// </summary>
    [InitializeOnLoad]
    public static class NodeOptions
    {
        /// <summary>The name the common group of attack places is shown by (its value is empty).</summary>
        public const string COMMON = "(common)";

        /// <summary>The tag an action state of an animator carries (handbook §20.2).</summary>
        public const string ACTION_TAG = "Action";

        private static List<string> _moves;
        private static List<string> _weapons;
        private static List<string> _actions;
        private static List<string> _pools;
        private static List<string> _knownPools;
        private static List<string> _abilities;
        private static List<string> _prefabKinds;
        private static List<string> _kinds;
        private static readonly Dictionary<(Type, bool), List<string>> Types = new();

        static NodeOptions()
        {
            EditorApplication.projectChanged += Forget;
            EditorApplication.hierarchyChanged += () => _kinds = null;     // an AI Object came or went in a scene
        }

        /// <summary>The lists are read anew on the next ask (the project changed).</summary>
        public static void Forget()
        {
            _moves = _weapons = _actions = _pools = _knownPools = _abilities = _prefabKinds = _kinds = null;
            Types.Clear();
        }

        /// <summary>
        /// The kinds of AI Object the scenes and the prefabs use (a What, a Zone): kept like the other lists — a node's
        /// picker is drawn on every pass of the canvas; the prefabs' part is read anew when the project changes, the
        /// scenes' when a scene does.
        /// </summary>
        public static List<string> Kinds() =>
            _kinds ??= ObjectKinds.Merged(ObjectKinds.InScenes(), _prefabKinds ??= ObjectKinds.InPrefabs());

        /// <summary>
        /// The groups of attack places a weapon's shot or a target names (not the graphs'): what a group a node asks for is
        /// checked against — a group only the graphs name is a typo, or a group no target keeps.
        /// </summary>
        public static List<string> KnownPools() =>
            _knownPools ??= PoolsOf(LoadAll<WeaponAsset>(), AttackPlacePools(), Array.Empty<string>());

        // ---------- the project's lists (cached) ----------

        /// <summary>The names of the moves of every weapon of the project.</summary>
        public static List<string> Moves() => _moves ??= MovesOf(LoadAll<WeaponAsset>());

        /// <summary>"Next", then the names of the project's weapons.</summary>
        public static List<string> Weapons() => _weapons ??= WeaponsOf(LoadAll<WeaponAsset>());

        /// <summary>Every action the project can show.</summary>
        public static List<string> Actions() =>
            _actions ??= ActionsOf(LoadAll<AnimatorController>(), LoadAll<WeaponAsset>(), LoadAll<AbilityAsset>());

        /// <summary>The groups of attack places the project names ("" — the common one — first).</summary>
        public static List<string> Pools() =>
            _pools ??= PoolsOf(LoadAll<WeaponAsset>(), AttackPlacePools(), PoolsInGraphs(LoadAll<BehaviorGraphAsset>()));

        /// <summary>The names of the project's abilities.</summary>
        public static List<string> Abilities() => _abilities ??= AbilitiesOf(LoadAll<AbilityAsset>());

        /// <summary>The project's tags.</summary>
        public static List<string> Tags() => new(InternalEditorUtility.tags);

        /// <summary>
        /// The full names of the types a [TypeName(base)] setting takes: every non-abstract type of that base. anyComponent —
        /// the components a Try Get Component may look for (step 91): every public one of the game and of Unity, not the editor's; the
        /// ones the game marked [AIReadable] (public or not) first.
        /// </summary>
        public static List<string> TypesOf(Type baseType, bool anyComponent = false)
        {
            if (baseType == null)
                return new List<string>();

            if (Types.TryGetValue((baseType, anyComponent), out List<string> names))
                return names;

            var marked = new List<string>();
            names = new List<string>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition || names.Contains(type.FullName) ||
                    marked.Contains(type.FullName))
                    continue;

                if (!anyComponent)
                    names.Add(type.FullName);
                else if (AIReadables.IsMarked(type))
                    marked.Add(type.FullName);
                else if (type.IsVisible && !IsEditors(type) && AIReadables.IsComponentType(type))
                    names.Add(type.FullName);
            }

            if (!anyComponent && !baseType.IsAbstract && !baseType.IsInterface)
                names.Add(baseType.FullName);
            names.Sort(StringComparer.Ordinal);
            marked.Sort(StringComparer.Ordinal);
            names.InsertRange(0, marked);
            Types[(baseType, anyComponent)] = names;
            return names;
        }

        // A type of the editor (Unity's or the project's): no object in a running game has it
        private static bool IsEditors(Type type)
        {
            string assembly = type.Assembly.GetName().Name;
            return assembly.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                   assembly.EndsWith(".Editor", StringComparison.Ordinal) ||
                   assembly.EndsWith("-Editor", StringComparison.Ordinal) ||
                   assembly.Contains(".Editor.") || assembly.Contains("Tests");
        }

        /// <summary>The list a picker setting is drawn from (null — the field is not a picker, or free text).</summary>
        public static List<string> For(FieldInfo field)
        {
            if (field == null || field.FieldType != typeof(string))
                return null;
            if (field.GetCustomAttribute<WeaponMoveNameAttribute>() != null)
                return Moves();
            if (field.GetCustomAttribute<WeaponNameAttribute>() != null)
                return Weapons();
            if (field.GetCustomAttribute<ActionNameAttribute>() != null)
                return Actions();
            if (field.GetCustomAttribute<PoolNameAttribute>() != null)
                return Pools();
            if (field.GetCustomAttribute<ObjectKindAttribute>() != null)
                return Kinds();
            if (field.GetCustomAttribute<TagNameAttribute>() != null)
                return Tags();
            if (field.GetCustomAttribute<TypeNameAttribute>() is { } typed)
                return TypesOf(typed.Base, typed.AnyComponent);

            return null;
        }

        /// <summary>
        /// What the empty value of a picker means, said in the list ("(first move)", "(common)", "(none)"); null — an empty
        /// value means nothing there (it is offered as "(none)" anyway, so a setting can be cleared).
        /// </summary>
        public static string EmptyMeaning(FieldInfo field)
        {
            if (field.GetCustomAttribute<WeaponMoveNameAttribute>() != null)
                return "(first move)";
            if (field.GetCustomAttribute<PoolNameAttribute>() != null)
                return COMMON;
            if (field.GetCustomAttribute<ObjectKindAttribute>() != null)
                return "(any kind)";
            if (field.GetCustomAttribute<TypeNameAttribute>() is { Empty: { } empty })
                return empty;
            return "(none)";
        }

        // ---------- the lists from what is given (tests give in-memory assets) ----------

        public static List<string> MovesOf(IEnumerable<WeaponAsset> weapons)
        {
            var names = new List<string>();
            foreach (WeaponAsset weapon in weapons)
            {
                if (weapon == null)
                    continue;

                foreach (WeaponMove move in weapon.Moves)
                    Add(names, move?.Name);
            }

            return names;
        }

        public static List<string> WeaponsOf(IEnumerable<WeaponAsset> weapons)
        {
            var names = new List<string> { SwitchWeaponTask.NEXT };
            foreach (WeaponAsset weapon in weapons)
            {
                if (weapon != null)
                    Add(names, weapon.name);
            }

            return names;
        }

        /// <summary>
        /// The actions: the states tagged Action of the controllers (in every layer and sub-machine), the actions of the
        /// weapons' moves and dashes, their drawing and sheathing, and the abilities' names (an ability is the action of its
        /// own name, handbook §16).
        /// </summary>
        public static List<string> ActionsOf(IEnumerable<AnimatorController> controllers, IEnumerable<WeaponAsset> weapons,
            IEnumerable<AbilityAsset> abilities)
        {
            var names = new List<string>();
            foreach (AnimatorController controller in controllers)
            {
                if (controller == null)
                    continue;

                foreach (AnimatorControllerLayer layer in controller.layers)
                    AddActions(layer.stateMachine, names);
            }

            foreach (WeaponAsset weapon in weapons)
            {
                if (weapon == null)
                    continue;

                foreach (WeaponMove move in weapon.Moves)
                    Add(names, move?.Action);
                foreach (WeaponDash dash in weapon.Dashes)
                    Add(names, dash?.Action);
                Add(names, weapon.DrawAction);
                Add(names, weapon.SheatheAction);
            }

            foreach (AbilityAsset ability in abilities)
            {
                if (ability != null)
                    Add(names, ability.Name);
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>The groups of attack places: the common one (""), the shots' groups, the targets' groups, the graphs'.</summary>
        public static List<string> PoolsOf(IEnumerable<WeaponAsset> weapons, IEnumerable<string> ofTargets,
            IEnumerable<string> ofGraphs)
        {
            var names = new List<string>();
            foreach (WeaponAsset weapon in weapons)
            {
                if (weapon != null && weapon.Shoots)
                    Add(names, weapon.Shot?.Pool);
            }

            foreach (string pool in ofTargets)
                Add(names, pool);
            foreach (string pool in ofGraphs)
                Add(names, pool);

            names.Sort(StringComparer.Ordinal);
            names.Insert(0, "");
            return names;
        }

        public static List<string> AbilitiesOf(IEnumerable<AbilityAsset> abilities)
        {
            var names = new List<string>();
            foreach (AbilityAsset ability in abilities)
            {
                if (ability != null)
                    Add(names, ability.Name);
            }

            return names;
        }

        /// <summary>
        /// The moves of the list the character's weapons do not have (a graph is shared: the list is the project's, the
        /// character the window was opened for is marked against it). Empty — no character, or it lacks none.
        /// </summary>
        public static HashSet<string> MissingFor(AICharacter character, IEnumerable<string> moves)
        {
            var missing = new HashSet<string>();
            if (character == null)
                return missing;

            var own = new HashSet<string>(MovesOf(character.Carried));
            foreach (string move in moves)
            {
                if (!string.IsNullOrEmpty(move) && !own.Contains(move))
                    missing.Add(move);
            }

            return missing;
        }

        /// <summary>
        /// The keys of a node nothing in the project has: a move of no weapon, an action nothing shows, a group of places
        /// nobody names (step 90: a warning on the node — a typo, or something renamed). Empty values are not keys.
        /// </summary>
        public static List<string> UnknownKeys(AgentNode node) => UnknownKeys(node, KnownFor);

        /// <summary>The same check against lists given (tests give their own; null list — the field is not checked).</summary>
        public static List<string> UnknownKeys(AgentNode node, Func<FieldInfo, List<string>> known)
        {
            var unknown = new List<string>();
            if (node == null || known == null)
                return unknown;

            foreach (FieldInfo field in node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                bool checks = field.GetCustomAttribute<WeaponMoveNameAttribute>() != null ||
                              field.GetCustomAttribute<ActionNameAttribute>() != null ||
                              field.GetCustomAttribute<PoolNameAttribute>() != null;
                if (!checks || field.GetValue(node) is not string value || value.Length == 0)
                    continue;

                List<string> options = known(field);
                if (options != null && options.Count > 0 && !options.Contains(value))
                    unknown.Add($"{ObjectNames.NicifyVariableName(field.Name)} '{value}' is not in the project");
            }

            return unknown;
        }

        // What a key is checked against: a group of places — only what a weapon or a target names (a graph naming it is
        // not proof: that is the typo being looked for); the rest — its list
        private static List<string> KnownFor(FieldInfo field) =>
            field.GetCustomAttribute<PoolNameAttribute>() != null ? KnownPools() : For(field);

        // ---------- inside ----------

        private static void Add(List<string> names, string name)
        {
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                names.Add(name);
        }

        // Every state of the machine and its sub-machines that carries the action tag
        private static void AddActions(AnimatorStateMachine machine, List<string> names)
        {
            if (machine == null)
                return;

            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state != null && child.state.tag == ACTION_TAG)
                    Add(names, child.state.name);
            }

            foreach (ChildAnimatorStateMachine sub in machine.stateMachines)
                AddActions(sub.stateMachine, names);
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var found = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is T typed && !found.Contains(typed))
                        found.Add(typed);
                }
            }

            return found;
        }

        // The groups of places the targets of the open scenes and the project's prefabs keep (Attack Slot Capacity). Only
        // the prefabs that use the component are loaded (asked by their dependencies: a project with packs of thousands of
        // prefabs is not loaded whole)
        private static IEnumerable<string> AttackPlacePools()
        {
            var capacities = new List<AttackSlotCapacity>(
                UnityEngine.Object.FindObjectsByType<AttackSlotCapacity>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Array.Exists(AssetDatabase.GetDependencies(path, false),
                        dependency => dependency.EndsWith("/" + nameof(AttackSlotCapacity) + ".cs", StringComparison.Ordinal)))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    capacities.AddRange(prefab.GetComponentsInChildren<AttackSlotCapacity>(true));
            }

            foreach (AttackSlotCapacity capacity in capacities)
            {
                if (capacity == null)
                    continue;

                SerializedProperty pools = new SerializedObject(capacity).FindProperty("_pools");
                for (int i = 0; pools != null && i < pools.arraySize; i++)
                {
                    string pool = pools.GetArrayElementAtIndex(i).FindPropertyRelative("Pool")?.stringValue;
                    if (!string.IsNullOrEmpty(pool))
                        yield return pool;
                }
            }
        }

        private static readonly Regex PoolKey = new("\"Pool\":\"([^\"]+)\"");

        // The groups the graphs of the project ask for (the Pool setting of their nodes, read from the records)
        private static IEnumerable<string> PoolsInGraphs(IEnumerable<BehaviorGraphAsset> graphs)
        {
            foreach (BehaviorGraphAsset graph in graphs)
            {
                if (graph == null)
                    continue;

                foreach (GraphRecord record in graph.Records)
                {
                    if (string.IsNullOrEmpty(record.Data))
                        continue;

                    foreach (Match match in PoolKey.Matches(record.Data))
                        yield return match.Groups[1].Value;
                }
            }
        }
    }
}
