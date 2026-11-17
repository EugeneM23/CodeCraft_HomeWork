using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Editor
{
    /// <summary>
    /// The blackboard of the behavior, as a panel on the canvas: the box of parameters the graph keeps (handbook §8 —
    /// the one channel of data between nodes; §31.2 — a live blackboard panel with values edited in place). One row
    /// per variable: its name, its kind and its value on the same line — the start value while editing, the agent's
    /// own value while the game runs. "+" adds a variable; a row dragged onto the canvas becomes a node of that
    /// variable, and its wire into a pin of a node is how the node takes it. An object variable shows no scene object:
    /// what fills it is the graph's (a sensor, a search — C18, C19). Under the variables, the Character section (step 90,
    /// C11 / C13): the character's numbers and states, read where they live and never copied here — a row dropped on a
    /// node that reads it is wired in as a Get or an Is (CharacterSection).
    /// </summary>
    public sealed class BlackboardPanel : UnityEditor.Experimental.GraphView.Blackboard
    {
        /// <summary>
        /// What a panel lists (15.11.2026, the user's word: the variables of the graph in a window of their own): the
        /// graph's variables (the Variables window) or the character's own numbers and states (the panel on the canvas).
        /// </summary>
        public enum Part
        {
            Variables,
            Character
        }

        private readonly Part _part;

        private static readonly Color ValueColor = new(0.25f, 0.85f, 0.4f);
        private static readonly Color QuietColor = new(0.6f, 0.6f, 0.6f);
        private const float VALUE_WIDTH = 96f;

        public const string OBJECTS = "Objects";
        public const string COUNTERS = "Counters";
        public const string UNUSED = "Not used";

        private readonly TextField _search = new() { tooltip = "Type a part of a name to find a variable" };
        private readonly VisualElement _list = new();
        private List<VariableUse> _uses = new();
        private string _filter = "";

        // Each row: its variable, what is shown while editing and the label that shows the live value instead in Play
        private readonly List<(VariableUse Use, VisualElement Edit, Label Live)> _rows = new();

        // The rows of the Character section: the row, its value before Play, its live value
        private readonly List<(CharacterRow Row, Label Design, Label Live)> _characterRows = new();
        private GraphDocument _document;

        public BlackboardPanel(GraphView canvas, Part part = Part.Variables) : base(canvas)
        {
            _part = part;
            title = part == Part.Variables ? "Variables" : "Character";
            subTitle = part == Part.Variables ? "the variables of this behavior" : "the character's own numbers and states";
            scrollable = true;
            SetPosition(new Rect(12f, 12f, 320f, 520f));
            addItemRequested = _ => ShowAddMenu();
            // A declared variable is renamed by a double click on its name: everything that names it follows
            editTextRequested = (_, element, name) =>
            {
                if (_document != null && element is BlackboardField renamed &&
                    !_document.RenameVariable(VariableOf(renamed), name))
                    Debug.LogWarning($"Blackboard: '{name}' is empty or already taken — the variable keeps its name");
            };
            _search.label = "Search";
            _search.labelElement.style.minWidth = 50;
            _search.style.marginLeft = _search.style.marginRight = 6;
            _search.RegisterValueChangedCallback(change => Filter(change.newValue));
            schedule.Execute(ShowValues).Every(200);

            // A row of the Character section dropped on the canvas: wired into the node it was dropped on
            if (part == Part.Character)
            {
                canvas?.RegisterCallback<DragUpdatedEvent>(OnCharacterDragUpdated);
                canvas?.RegisterCallback<DragPerformEvent>(OnCharacterDragPerform);
                addItemRequested = null;
                schedule.Execute(() =>
                {
                    Button add = this.Q<Button>("addButton");
                    if (add != null)
                        add.style.display = DisplayStyle.None;   // nothing to add: the character says these itself
                });
            }
        }

        /// <summary>The variable a row of the panel stands for (the canvas asks when a row is dropped on it).</summary>
        public static string VariableOf(BlackboardField field) => field?.userData as string;

        /// <summary>The row of the Character section a field stands for; null — a variable's row.</summary>
        public static CharacterRow CharacterRowOf(BlackboardField field) => field?.userData as CharacterRow;

        /// <summary>The rows the panel shows now: the variables' names, then the Character section's (tests read it).</summary>
        public IEnumerable<string> RowNames
        {
            get
            {
                foreach ((VariableUse use, VisualElement _, Label _) in _rows)
                    yield return use.Name;
                foreach ((CharacterRow row, Label _, Label _) in _characterRows)
                    yield return row.Name;
            }
        }

        public void Refresh(GraphDocument document)
        {
            _document = document;
            _uses = BlackboardUsage.Collect(document);
            Clear();
            Add(_search);
            Add(_list);
            ShowRows();
        }

        /// <summary>Only the variables whose name has this text in it are listed (empty — all of them).</summary>
        public void Filter(string text)
        {
            _filter = text ?? "";
            ShowRows();
        }

        /// <summary>
        /// The group a variable is listed in: the objects of the scene together, the counters together, and every
        /// number or flag under the node that reads it ("Attack": its range, its damage).
        /// </summary>
        public static string GroupOf(VariableUse use)
        {
            if (!string.IsNullOrEmpty(use.Group))
                return use.Group;

            if (use.Type == VariableType.Transform)
                return OBJECTS;

            if (!use.Declared)
                return COUNTERS;

            return use.Readers.Count > 0 ? use.Readers[0] : UNUSED;
        }

        // The variables that pass the search, group by group; then the character's own numbers and states
        private void ShowRows()
        {
            _rows.Clear();
            _characterRows.Clear();
            _list.Clear();
            var sections = new Dictionary<string, BlackboardSection>();
            foreach (VariableUse use in _part == Part.Variables ? _uses : new List<VariableUse>())
            {
                if (_filter.Length > 0 && use.Name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string group = GroupOf(use);
                if (!sections.TryGetValue(group, out BlackboardSection section))
                {
                    section = new BlackboardSection { title = group };
                    sections[group] = section;
                    _list.Add(section);
                }

                section.Add(Row(use));
            }

            var character = new BlackboardSection { title = CharacterSection.TITLE };
            foreach (CharacterRow row in _part == Part.Character ? CharacterSection.Rows : System.Array.Empty<CharacterRow>())
            {
                if (_filter.Length == 0 || row.Name.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    character.Add(Row(row));
            }

            if (character.childCount > 0)
                _list.Add(character);

            ShowValues();
        }

        // One line: the name (dragged onto the canvas as a node of the variable), its kind, its value
        private VisualElement Row(VariableUse use)
        {
            VisualElement row = Line();
            var field = new BlackboardField(null, use.Name, use.Kind) { userData = use.Name, tooltip = WhoTouches(use) };
            Tint(row, field, ColorOf(use.Type));
            bool own = use.Declared && string.IsNullOrEmpty(use.Group);
            if (!own)
            {
                // only the graph's own variables are renamed and deleted here
                field.capabilities &= ~(Capabilities.Renamable | Capabilities.Deletable);
            }
            else
            {
                // Delete (the key, or the row's menu): the variable goes with its nodes, wires and the fields naming it
                field.AddManipulator(new ContextualMenuManipulator(evt =>
                    evt.menu.AppendAction("Delete Variable", _ =>
                    {
                        if (_document != null && _document.DeleteVariable(use.Name))
                            Debug.Log($"Blackboard: '{use.Name}' deleted");
                    })));
            }
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            row.Add(field);

            VisualElement edit = StartValue(use);
            edit.style.width = VALUE_WIDTH;
            edit.style.flexShrink = 0;
            row.Add(edit);

            Label live = LiveLabel();
            row.Add(live);
            _rows.Add((use, edit, live));
            return row;
        }

        // One line of the Character section: the name (dropped on a node that reads it: a Get or an Is), its kind, its
        // value — read-only
        private VisualElement Row(CharacterRow character)
        {
            VisualElement row = Line();
            var field = new BlackboardField(null, character.Name, character.Kind)
            {
                userData = character,
                tooltip = character.IsState
                    ? "A state of the character: dropped on a Branch or a decision, it is asked there (Is)"
                    : "A number of the character: dropped on a Compare, a Set / Add or a Debug Log, it is read there (Get)"
            };
            Tint(row, field, ColorOf(character.IsState ? VariableType.Bool : VariableType.Float));
            field.capabilities &= ~(Capabilities.Renamable | Capabilities.Deletable);
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            row.Add(field);

            Label design = Quiet(CharacterSection.DesignValue(character, ElementInspector.Character));
            design.style.width = VALUE_WIDTH;
            design.style.flexShrink = 0;
            row.Add(design);

            Label live = LiveLabel();
            row.Add(live);
            _characterRows.Add((character, design, live));
            return row;
        }

        /// <summary>
        /// The colour of a kind of data, the same as its pins and wires on the canvas (as in Unreal): a number green, a flag
        /// red, an object blue.
        /// </summary>
        public static Color ColorOf(VariableType kind) => GraphViewCanvas.PinColor(ValueFields.PinType(kind));

        // A row says its kind by colour: a dot before the name, and the kind's word in that colour
        private static void Tint(VisualElement row, BlackboardField field, Color color)
        {
            var dot = new VisualElement { name = "kind-dot", pickingMode = PickingMode.Ignore };
            dot.style.width = dot.style.height = 9;
            dot.style.marginLeft = 4;
            dot.style.marginRight = 2;
            dot.style.flexShrink = 0;
            dot.style.backgroundColor = color;
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 5;
            row.Add(dot);
            Label kind = field.Q<Label>("typeLabel");
            if (kind != null)
                kind.style.color = color;
        }

        private static VisualElement Line()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingRight = 4;
            return row;
        }

        private static Label LiveLabel()
        {
            var live = new Label { pickingMode = PickingMode.Ignore };
            live.style.width = VALUE_WIDTH;
            live.style.flexShrink = 0;
            live.style.color = ValueColor;
            live.style.unityTextAlign = TextAnchor.MiddleLeft;
            live.style.display = DisplayStyle.None;
            return live;
        }

        // What a variable is before the game runs: the start value of a number or a flag — or, for what the graph only
        // writes while it runs (an object a sensor or a search fills), a quiet note. The character gives no scene objects
        // to the graph any more (step 90, C18): there is no object to pick here
        private VisualElement StartValue(VariableUse use)
        {
            if (!string.IsNullOrEmpty(use.Group))
                return Quiet(use.Type == VariableType.Bool ? "—" : ArrowPhrases.Number(use.OwnValue));

            // An object is what the graph finds or sees while it runs: no scene object is given to it here (step 90, C18)
            if (use.Type == VariableType.Transform)
                return Quiet(use.Writers.Count > 0 ? "in play" : "—");

            if (!use.Declared || _document == null)
                return Quiet("in play");

            switch (_document.DefaultOf(use.Name))
            {
                case float number:
                {
                    var input = new FloatField { value = number, isDelayed = true };
                    input.RegisterValueChangedCallback(change => _document.SetDefault(use.Name, change.newValue));
                    return input;
                }
                case int whole:
                {
                    var input = new IntegerField { value = whole, isDelayed = true };
                    input.RegisterValueChangedCallback(change => _document.SetDefault(use.Name, change.newValue));
                    return input;
                }
                case double wide:
                {
                    var input = new DoubleField { value = wide, isDelayed = true };
                    input.RegisterValueChangedCallback(change => _document.SetDefault(use.Name, change.newValue));
                    return input;
                }
                case bool flag:
                {
                    var input = new Toggle { value = flag };
                    input.RegisterValueChangedCallback(change => _document.SetDefault(use.Name, change.newValue));
                    return input;
                }
                default:
                    return Quiet("—");
            }
        }

        private static Label Quiet(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.style.color = QuietColor;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        private static string WhoTouches(VariableUse use) =>
            $"Written by: {(use.Writers.Count > 0 ? string.Join(", ", use.Writers) : "nobody")}\n" +
            $"Read by: {(use.Readers.Count > 0 ? string.Join(", ", use.Readers) : "nobody")}";

        // While the game runs: what each variable holds for the agent the window follows, in place of the start value —
        // and what the character says about itself now
        private void ShowValues()
        {
            AICharacter agent = ElementInspector.Agent;
            bool live = agent != null && agent.Context != null;
            foreach ((VariableUse use, VisualElement edit, Label value) in _rows)
            {
                edit.style.display = live ? DisplayStyle.None : DisplayStyle.Flex;
                value.style.display = live ? DisplayStyle.Flex : DisplayStyle.None;
                if (live)
                    value.text = ValueText(agent.Context.Blackboard, use);
            }

            foreach ((CharacterRow row, Label design, Label value) in _characterRows)
            {
                design.style.display = live ? DisplayStyle.None : DisplayStyle.Flex;
                value.style.display = live ? DisplayStyle.Flex : DisplayStyle.None;
                if (live)
                    value.text = CharacterSection.LiveValue(row, agent);
            }
        }

        private static string ValueText(AIKit.Blackboard board, VariableUse use)
        {
            switch (use.Type)
            {
                case VariableType.Transform:
                    Transform target = board.Get<Transform>(use.Name);
                    return target != null ? target.name : "nothing";
                case VariableType.Bool:
                    return board.Get(use.Name, false) ? "yes" : "no";
                case VariableType.Int:
                    return board.Get(use.Name, 0).ToString();
                case VariableType.Double:
                    return board.Get(use.Name, 0d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                case VariableType.String:
                    return board.Get(use.Name, "") ?? "";
                case VariableType.Vector3:
                    return board.Get(use.Name, Vector3.zero).ToString("0.#");
                default:
                    return ArrowPhrases.Number(board.Get(use.Name, 0f));
            }
        }

        private void ShowAddMenu()
        {
            if (_document == null)
                return;

            var menu = new GenericMenu();
            Offer(menu, "Transform (a thing of the scene)", "Object", VariableType.Transform);
            Offer(menu, "float (a number)", "Number", VariableType.Float);
            Offer(menu, "int (a whole number)", "Count", VariableType.Int);
            Offer(menu, "double (a number, more precise)", "Value", VariableType.Double);
            Offer(menu, "bool (yes or no)", "Is On", VariableType.Bool);       // a yes / no is named "Is …"
            menu.ShowAsContext();
        }

        private void Offer(GenericMenu menu, string label, string baseName, VariableType type) =>
            menu.AddItem(new GUIContent(label), false, () =>
            {
                _document.DeclareUnique(baseName, type);
                _document.Touch("Add Variable");
            });

        // ---------- a row of the Character section dragged onto the canvas ----------

        // The row of the Character section being dragged (null — something else is dragged)
        private static CharacterRow DraggedCharacterRow()
        {
            if (DragAndDrop.GetGenericData("DragSelection") is not List<ISelectable> dragged)
                return null;

            foreach (ISelectable item in dragged)
            {
                if (item is BlackboardField field && CharacterRowOf(field) != null)
                    return CharacterRowOf(field);
            }

            return null;
        }

        private void OnCharacterDragUpdated(DragUpdatedEvent evt)
        {
            if (_document == null || DraggedCharacterRow() == null)
                return;

            DragAndDrop.visualMode = ReaderUnder(evt.target as VisualElement, out _, out _)
                ? DragAndDropVisualMode.Link
                : DragAndDropVisualMode.Rejected;
            evt.StopPropagation();
        }

        private void OnCharacterDragPerform(DragPerformEvent evt)
        {
            CharacterRow row = DraggedCharacterRow();
            if (_document == null || row == null)
                return;

            DragAndDrop.AcceptDrag();
            evt.StopPropagation();
            bool found = ReaderUnder(evt.target as VisualElement, out object reader, out Vector2 at);
            if (!found || CharacterSection.Wire(_document, row, reader, at + new Vector2(-220f, 60f)) == null)
                Debug.Log($"Blackboard: drop '{row.Name}' on a node that reads it — " + (row.IsState
                    ? "a Branch or a decision (it is asked there as an Is)"
                    : "a Compare, a Set / Add… or a Debug Log (it is read there as a Get)"));
        }

        // The element of the graph whose node is under the mouse (a state, or a condition node), by the place the
        // document keeps for it; false — no node there
        private bool ReaderUnder(VisualElement under, out object reader, out Vector2 at)
        {
            reader = null;
            at = Vector2.zero;
            Node node = null;
            for (VisualElement element = under; element != null && node == null; element = element.parent)
                node = element as Node;
            if (node == null || _document?.Graph == null)
                return false;

            Vector2 place = node.GetPosition().position;
            at = place;
            var states = new List<AgentState>();
            _document.Graph.CollectStates(states);
            foreach (AgentState state in states)
            {
                if (state != _document.Graph.Root && (_document.PositionOf(state) - place).sqrMagnitude < 4f)
                {
                    reader = state;
                    return true;
                }
            }

            AgentCondition found = null;
            _document.Graph.VisitNodes(element =>
            {
                if (found == null && element is AgentCondition condition &&
                    (_document.PositionOfNode(condition) - place).sqrMagnitude < 4f)
                    found = condition;
            });
            reader = found;
            return found != null;
        }
    }
}
