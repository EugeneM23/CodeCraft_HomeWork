using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// The settings of a node drawn on the canvas from the node itself (no editor per node class): numbers, toggles,
    /// enums, vectors, blackboard values (a constant or a declared variable), variable names (a popup of the declared
    /// ones), project assets (an ability, a weapon), and every text setting that names something — a move, an action, a
    /// group of places, a tag, a type — as a list to pick from, never a text field (step 90, C5; handbook §32.2). Every
    /// change is written back through the document.
    /// </summary>
    public static class ElementInspector
    {
        private static readonly HashSet<object> Open = new();

        /// <summary>
        /// The character the graph window was opened for (null — the asset alone): the lists mark what its weapons lack;
        /// the blackboard shows its values.
        /// </summary>
        public static AICharacter Character;

        /// <summary>
        /// The agent whose run the graph window shows now (Play Mode; null — nobody runs this graph in the window):
        /// a node that has something live to say about its agent asks it here.
        /// </summary>
        public static AICharacter Agent;

        /// <summary>
        /// On — fields are drawn the short way the canvas needs: a blackboard value is a plain number, an object
        /// variable is just its pin.
        /// </summary>
        public static bool Compact;

        /// <summary>
        /// On — the element being drawn has data pins on the canvas: an object comes only by a wire into its pin, so the
        /// field is not drawn in the body at all.
        /// </summary>
        public static bool Wired;

        /// <summary>The canvas draws the numbers and flags of a node in their pins: the body leaves them out.</summary>
        public static bool ValuesInPins;

        /// <summary>A list longer than this opens as a window with search (a button by its popup).</summary>
        private const int LONG_LIST = 16;

        /// <summary>The fields of an element that name a Transform variable: each is a data pin on the canvas.</summary>
        public static IEnumerable<FieldInfo> ObjectFields(object element)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public;
            foreach (FieldInfo field in element.GetType().GetFields(FLAGS))
            {
                if (field.FieldType == typeof(string) &&
                    field.GetCustomAttribute<VariableNameAttribute>() is { HasType: true, Type: VariableType.Transform })
                    yield return field;
            }
        }

        /// <summary>The settings of an element the body draws (ownOnly — not the levels every node has).</summary>
        public static void Fields(GraphDocument document, object element, bool ownOnly = false)
        {
            foreach (FieldInfo field in Drawn(element, ownOnly))
            {
                object value = field.GetValue(element);
                object next = Value(document, element, field, ObjectNames.NicifyVariableName(field.Name), value);
                field.SetValue(element, next);           // a boxed struct (a blackboard value) changes in place
            }
        }

        /// <summary>
        /// How wide the labels of the element's settings must be for the longest to show whole ("Needs Attack Place", not
        /// "Needs Attack Plac"), in pixels, between min and max.
        /// </summary>
        public static float LabelWidthFor(object element, bool ownOnly, float min = 60f, float max = 220f)
        {
            float widest = min;
            GUIStyle style = EditorStyles.label;
            foreach (FieldInfo field in Drawn(element, ownOnly))
            {
                float width = style.CalcSize(new GUIContent(ObjectNames.NicifyVariableName(field.Name))).x + 8f;
                widest = Mathf.Max(widest, width);
            }

            return Mathf.Min(widest, max);
        }

        /// <summary>The settings Fields draws, in order (the base levels first).</summary>
        public static IEnumerable<FieldInfo> Drawn(object element, bool ownOnly)
        {
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var levels = new List<Type>();
            for (Type type = element.GetType(); type != null && type != typeof(object); type = type.BaseType)
                levels.Insert(0, type);

            foreach (Type level in levels)
            {
                if (ownOnly && (level == typeof(AgentNode) || level == typeof(AgentTask) || level == typeof(AgentValue) ||
                                level == typeof(AgentCondition) || level == typeof(SensorTask)))
                    continue;                           // the name and the flags every node has: not on the canvas

                foreach (FieldInfo field in level.GetFields(FLAGS | BindingFlags.DeclaredOnly))
                {
                    bool shown = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
                    if (field.IsInitOnly || field.IsStatic || !shown)
                        continue;

                    if (field.Name == nameof(AgentNode.Id) || field.Name == nameof(Transition.TargetId))
                        continue;

                    if (element is IReadsMembers reads && !reads.Shows(field.Name))
                        continue;                       // a Get / Is: its own settings, or the member of a Cast's type

                    // A setting for graphs built in code: the canvas does not show it (the side inspector does)
                    if (Compact && field.GetCustomAttribute<HideInInspector>() != null)
                        continue;

                    if (GraphFields.IsElement(field.FieldType) || IsElementList(field.FieldType))
                        continue;                       // structure: drawn as wires into pins

                    if (ValuesInPins && ValueFields.IsValue(field) && ValueFields.KindOf(field.FieldType) != null)
                        continue;                       // the canvas draws it in its pin

                    if (Compact && Wired && field.FieldType == typeof(string) &&
                        field.GetCustomAttribute<VariableNameAttribute>() is { HasType: true, Type: VariableType.Transform })
                        continue;                       // an object comes only by the wire into its pin: nothing hidden in the node

                    yield return field;
                }
            }
        }

        /// <summary>One field of an element by its name (the canvas shows a few fields of a sensor, not all of them).</summary>
        public static void Field(GraphDocument document, object element, string fieldName, string label)
        {
            FieldInfo field = element.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null)
                return;

            field.SetValue(element, Value(document, element, field, label, field.GetValue(element)));
        }

        private static bool IsElementList(Type type) =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) &&
            GraphFields.IsElement(type.GetGenericArguments()[0]);

        private static object Value(GraphDocument document, object element, FieldInfo field, string label, object value)
        {
            Type type = field.FieldType;
            if (type == typeof(string) && field.GetCustomAttribute<VariableNameAttribute>() is { } variable)
                return VariablePopup(document, label, (string)value, variable.HasType ? variable.Type : null);
            if (type == typeof(float))
                return EditorGUILayout.FloatField(label, (float)value);
            if (type == typeof(int))
                return EditorGUILayout.IntField(label, (int)value);
            if (type == typeof(bool))
                return EditorGUILayout.Toggle(label, (bool)value);
            if (type == typeof(string) && field.GetCustomAttribute<TagNameAttribute>() != null)
                return PickerDrawer.TagToStore((string)value,
                    EditorGUILayout.TagField(label, PickerDrawer.ShownTag((string)value)));    // drawn: written only if picked
            if (type == typeof(string) && field.GetCustomAttribute<MemberNameAttribute>() != null)
                return MemberPopup(document, element, field, label, (string)value);
            if (type == typeof(string) && NodeOptions.For(field) is { } options)
                return OptionPopup(document, element, field, label, (string)value, options);
            if (type == typeof(string))
                return EditorGUILayout.TextField(label, (string)value);     // free text ([FreeText]: a Debug Log message)
            if (type.IsEnum)
                return EditorGUILayout.EnumPopup(label, (Enum)value);
            if (type == typeof(Vector2))
                return EditorGUILayout.Vector2Field(label, (Vector2)value);
            if (type == typeof(Vector3))
                return EditorGUILayout.Vector3Field(label, (Vector3)value);
            if (type == typeof(Color))
                return EditorGUILayout.ColorField(label, (Color)value);
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, type, false);  // a project asset only
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BlackboardValue<>))
                return BlackboardValueField(document, label, value);
            if (type == typeof(List<float>))
                return FloatList(label, (List<float>)value);
            if (value is IList list)
            {
                EditorGUILayout.LabelField(label, $"{list.Count} entries (edit in the asset's inspector)");
                return value;
            }

            if (value != null && type.IsClass && type.IsSerializable)
            {
                if (Foldout(value, label))
                {
                    EditorGUI.indentLevel++;
                    Fields(document, value);
                    EditorGUI.indentLevel--;
                }

                return value;
            }

            EditorGUILayout.LabelField(label, value?.ToString() ?? "—");
            return value;
        }

        /// <summary>
        /// The words a picker shows for its value (pure, for tests): the empty value says what it means ("(common)"), a
        /// value the project has none of keeps its name marked "(not found)", an option the character the window was
        /// opened for lacks is marked "(not on its weapons)".
        /// </summary>
        public static List<string> OptionWords(IReadOnlyList<string> options, string value, string emptyMeaning,
            ICollection<string> lacking, out int selected, out List<string> values)
        {
            values = new List<string> { "" };
            var words = new List<string> { emptyMeaning ?? "(none)" };
            foreach (string option in options)
            {
                if (string.IsNullOrEmpty(option) || values.Contains(option))
                    continue;

                values.Add(option);
                words.Add(lacking != null && lacking.Contains(option) ? $"{option} (not on its weapons)" : option);
            }

            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
                words.Add($"{value} (not found)");     // kept as it is, and said: a typo, or a thing renamed
            }

            selected = Mathf.Max(0, values.IndexOf(value ?? ""));
            return words;
        }

        /// <summary>
        /// What a member picker offers (pure, for tests): the members of the reader's type it reads — flags for an Is,
        /// numbers for a Get — by their own names, shown by their labels; first the empty value ("(is a <type>)" for an Is:
        /// the cast itself; "(none)" for a Get). A name the type has none of stays, marked "(not found)".
        /// </summary>
        public static List<(string Value, string Word)> MemberOptions(IReadsMembers reader, string value)
        {
            var options = new List<(string, string)>();
            Type type = reader?.MemberSource;
            options.Add(("", reader is { ReadsFlags: true } ? $"(has a {AIReadables.Words(type)})" : "(none)"));
            foreach (ReadableMember member in AIReadables.MembersOf(type))
            {
                if (member.IsFlag == (reader?.ReadsFlags ?? false))
                    options.Add((member.Name, member.Label));
            }

            if (!string.IsNullOrEmpty(value) && !options.Exists(option => option.Item1 == value))
                options.Add((value, $"{AIReadables.Spaced(value)} (not found)"));
            return options;
        }

        // A member of the type of the Cast wired into the node
        private static string MemberPopup(GraphDocument document, object element, FieldInfo field, string label, string value)
        {
            List<(string Value, string Word)> options = MemberOptions(element as IReadsMembers, value);
            int selected = Mathf.Max(0, options.FindIndex(option => option.Value == (value ?? "")));
            string[] shown = options.Select(option => option.Word.Replace('/', '∕')).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                int picked = EditorGUILayout.Popup(label, selected, shown);
                if (options.Count > LONG_LIST && GUILayout.Button(new GUIContent("⌕", "Search the list"),
                        EditorStyles.miniButton, GUILayout.Width(20f)))
                {
                    var items = options.Select(option => (option.Word.Replace('/', '∕'), option.Value)).ToList();
                    Vector2 screen = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    SearchWindow.Open(new SearchWindowContext(screen), NodeLibrary.Of(label, items, chosen =>
                    {
                        field.SetValue(element, chosen);
                        document?.TouchQuietly();
                    }));
                }

                return picked >= 0 && picked < options.Count ? options[picked].Value : value;
            }
        }

        // A text setting picked from a list of the project; a long list has a button that opens it with search
        private static string OptionPopup(GraphDocument document, object element, FieldInfo field, string label,
            string value, List<string> options)
        {
            ICollection<string> lacking = field.GetCustomAttribute<WeaponMoveNameAttribute>() != null
                ? NodeOptions.MissingFor(Character, options)
                : null;
            List<string> words = OptionWords(options, value, NodeOptions.EmptyMeaning(field), lacking, out int selected,
                out List<string> values);

            using (new EditorGUILayout.HorizontalScope())
            {
                // A name with a slash would open a sub-menu of the popup: shown with another slash
                string[] shown = words.Select(word => word.Replace('/', '∕')).ToArray();
                int picked = EditorGUILayout.Popup(label, selected, shown);
                if (values.Count > LONG_LIST && GUILayout.Button(new GUIContent("⌕", "Search the list"),
                        EditorStyles.miniButton, GUILayout.Width(20f)))
                {
                    var items = new List<(string Path, string Id)>();
                    for (int i = 0; i < values.Count; i++)
                        items.Add((words[i].Replace('/', '∕'), values[i]));

                    Vector2 screen = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                    SearchWindow.Open(new SearchWindowContext(screen), NodeLibrary.Of(label, items, chosen =>
                    {
                        field.SetValue(element, chosen);
                        document?.TouchQuietly();
                    }));
                }

                return picked >= 0 && picked < values.Count ? values[picked] : value;
            }
        }

        private static object BlackboardValueField(GraphDocument document, string label, object boxed)
        {
            Type type = boxed.GetType();
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;
            FieldInfo useVariable = type.GetField("_useVariable", FLAGS);
            FieldInfo name = type.GetField("_name", FLAGS);
            FieldInfo scope = type.GetField("_scope", FLAGS);
            FieldInfo constant = type.GetField("_constant", FLAGS);

            if (Compact)
            {
                // On the canvas: the node's own number — or, when a wire from a blackboard variable comes into it, the
                // name of that variable. The wire is the only way a node takes a variable: no buttons that convert
                if ((bool)useVariable.GetValue(boxed))
                    EditorGUILayout.LabelField(label, "← " + (string)name.GetValue(boxed));
                else
                    constant.SetValue(boxed, new FieldHolder { Type = constant.FieldType }.Draw(label, constant.GetValue(boxed)));

                return boxed;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool uses = EditorGUILayout.ToggleLeft("$", (bool)useVariable.GetValue(boxed), GUILayout.Width(28f));
                useVariable.SetValue(boxed, uses);
                if (uses)
                {
                    name.SetValue(boxed, VariablePopup(document, label, (string)name.GetValue(boxed)));
                    scope.SetValue(boxed, EditorGUILayout.EnumPopup((BlackboardScope)scope.GetValue(boxed),
                        GUILayout.Width(70f)));
                }
                else
                {
                    var holder = new FieldHolder { Type = constant.FieldType };
                    constant.SetValue(boxed, holder.Draw(label, constant.GetValue(boxed)));
                }
            }

            return boxed;
        }

        private sealed class FieldHolder
        {
            public Type Type;

            public object Draw(string label, object value)
            {
                if (Type == typeof(float))
                    return EditorGUILayout.FloatField(label, (float)value);
                if (Type == typeof(int))
                    return EditorGUILayout.IntField(label, (int)value);
                if (Type == typeof(bool))
                    return EditorGUILayout.Toggle(label, (bool)value);
                if (Type == typeof(string))
                    return EditorGUILayout.TextField(label, (string)value);
                if (Type == typeof(Vector3))
                    return EditorGUILayout.Vector3Field(label, (Vector3)value);
                EditorGUILayout.LabelField(label, value?.ToString() ?? "—");
                return value;
            }
        }

        private static string VariablePopup(GraphDocument document, string label, string current,
            VariableType? type = null)
        {
            List<string> names = document.Graph.Variables.Where(v => type == null || v.Type == type)
                .Select(v => v.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            // The numbers the character's own parts keep on the blackboard (speed, an ability's damage) are variables too
            if (type == null || type == VariableType.Float)
            {
                foreach (VariableUse own in BlackboardUsage.OfCharacter(Character))
                {
                    if (!names.Contains(own.Name))
                        names.Add(own.Name);
                }
            }

            if (!string.IsNullOrEmpty(current) && !names.Contains(current))
                names.Insert(0, current);                 // undeclared: shown, and the validator names it
            names.Insert(0, "—");
            int index = Mathf.Max(0, names.IndexOf(current ?? "—"));
            int picked = EditorGUILayout.Popup(label, index, names.ToArray());
            return picked == 0 ? "" : names[picked];
        }

        private static List<float> FloatList(string label, List<float> list)
        {
            list ??= new List<float>();
            EditorGUILayout.LabelField(label);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
                list[i] = EditorGUILayout.FloatField($"#{i}", list[i]);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+"))
                    list.Add(1f);
                if (GUILayout.Button("−") && list.Count > 0)
                    list.RemoveAt(list.Count - 1);
            }

            EditorGUI.indentLevel--;
            return list;
        }

        private static bool Foldout(object key, string label)
        {
            bool open = Open.Contains(key);
            bool next = EditorGUILayout.Foldout(open, label, true);
            if (next && !open)
                Open.Add(key);
            else if (!next && open)
                Open.Remove(key);
            return next;
        }
    }
}
