using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Editor
{
    /// <summary>
    /// The canvas on GraphView — the program drawn (step 89): the Brain, its decisions (each with its Condition and On
    /// Tick pins), their chains of blocks with their settings right on them, the sensors, the conditions and the value
    /// nodes wired into their pins. A node is called by its type unless the user labels it (double click the title). A
    /// right click on an empty place, Space and a pin let go over an empty place open the one list of nodes with search
    /// (NodeMenu). An arrow back across the canvas ends at a compact link label docked at its pin; each wire of the Brain
    /// leaves the Brain's frame from a point of its own (step 90). In Play Mode the running node pulses and shows its time,
    /// and the arrow the agent has just taken flashes. Everything goes through the document.
    /// </summary>
    public sealed class GraphViewCanvas : GraphView, IGraphCanvas
    {
        private const float FLASH_SECONDS = 0.9f;
        private const float ENTER_SECONDS = 0.35f;

        private static readonly Color ActiveColor = new(0.25f, 0.85f, 0.4f);
        private static readonly Color ActiveDimColor = new(0.12f, 0.5f, 0.22f);
        private static readonly Color FlashColor = new(1f, 0.8f, 0.2f);
        private static readonly Color DataColor = new(0.4f, 0.8f, 1f);

        // The colour of a data pin and of its wire, by what it carries: a number, a flag, an object
        /// <summary>The colour of a pin, a wire and a row of the blackboard by the kind of data it carries.</summary>
        public static Color PinColor(Type pinType) => TypeColor(pinType);

        private static Color TypeColor(Type pinType) =>
            pinType == typeof(bool) ? new Color(0.86f, 0.3f, 0.34f)
            : pinType == typeof(Transform) ? new Color(0.35f, 0.6f, 1f)
            : pinType == typeof(float) ? new Color(0.62f, 0.86f, 0.3f)
            : DataColor;

        /// <summary>
        /// The value of a pin with no wire in it, typed right in the pin — gone once a wire comes in (as in Unreal).
        /// A number or a flag of a node; the node is written back without a rebuild.
        /// </summary>
        private static void AddPinField(Port port, object owner, System.Reflection.FieldInfo field, GraphDocument document)
        {
            object boxed = field.GetValue(owner);
            if (ValueFields.Uses(boxed))
                return;

            void Store(object value)
            {
                field.SetValue(owner, ValueFields.ToConstant(field.GetValue(owner), value));
                document.TouchQuietly();
            }

            VisualElement editor;
            switch (ValueFields.Constant(boxed))
            {
                case float number:
                    var numberField = new FloatField { value = number };
                    numberField.RegisterValueChangedCallback(change => Store(change.newValue));
                    editor = numberField;
                    break;
                case int whole:
                    var wholeField = new IntegerField { value = whole };
                    wholeField.RegisterValueChangedCallback(change => Store(change.newValue));
                    editor = wholeField;
                    break;
                case bool flag:
                    var flagField = new Toggle { value = flag };
                    flagField.RegisterValueChangedCallback(change => Store(change.newValue));
                    editor = flagField;
                    break;
                default:
                    return;
            }

            editor.style.width = editor is Toggle ? 16f : 44f;
            editor.style.marginLeft = 2f;
            port.Add(editor);
        }

        /// <summary>A link (an arrow drawn short, to a label): the label, the mark at the other end.</summary>
        private static readonly Color LinkColor = new(0.3f, 0.26f, 0.42f);

        /// <summary>The title of the node a picked link points at.</summary>
        private static readonly Color PointedColor = new(0.56f, 0.4f, 0.9f);

        private const string COPY_PREFIX = "AIKitVariables:";

        // The colour of an arrow says why it is taken: the node's work came out well, came out badly, or a condition
        // (a sensor's "Detected" too) became true. The pin the arrow leaves has the same colour
        private static readonly Color SuccessColor = new(0.42f, 0.82f, 0.46f);
        private static readonly Color FailureColor = new(0.93f, 0.43f, 0.38f);
        private static readonly Color ConditionColor = new(0.96f, 0.8f, 0.3f);
        private static readonly Color MarkerColor = new(0.5f, 0.5f, 0.5f);

        /// <summary>What a wire that does not belong to the picked node fades to (the canvas behind it).</summary>
        private static readonly Color FadeColor = new(0.17f, 0.17f, 0.17f);

        /// <summary>The pin a variable is filled through (a sensor's "Object", a variable node's input): orange; reading is blue.</summary>
        private static readonly Color FillColor = new(1f, 0.6f, 0.15f);

        private readonly Dictionary<int, StateNode> _nodes = new();
        private readonly Dictionary<SensorTask, SensorNode> _sensors = new();
        private readonly Dictionary<GraphVariableNode, VariableNodeView> _variables = new();

        // A stretch of a wire → the owner of the wire and the number of the stretch (a new point goes in at that index)
        private readonly Dictionary<Edge, (object Wire, int Index)> _segments = new();
        private readonly List<LiveEdge> _edges = new();
        private GraphDocument _document;
        private bool _rebuilding;

        private Transition _flashed;
        private float _flashStamp = float.NaN;
        private double _flashStart = double.MinValue;
        private int _leafId;
        private double _enterTime;

        public GraphViewCanvas()
        {
            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            Insert(0, new GridBackground());
            style.flexGrow = 1;
            graphViewChanged = OnChanged;
            // Space: the one list of nodes, at the mouse (a right click on an empty place opens the same one)
            nodeCreationRequest = context =>
                OpenList(context.screenMousePosition, ToGraph(context.screenMousePosition), "Add Node",
                    NodeMenu.Everything(_document), null);
            RegisterCallback<MouseDownEvent>(OnMouseDown, TrickleDown.TrickleDown);

            // A wire pulled from the rim of the Brain's frame follows the mouse until it is let go
            RegisterCallback<MouseMoveEvent>(OnRimMove);
            RegisterCallback<MouseUpEvent>(OnRimUp);

            // The character's own numbers and states on the canvas (a row dropped on a node is read there as a Get or an
            // Is); the graph's variables are in the Variables window (15.11.2026), a row of it dropped here becomes one
            // more node of that variable
            _board = new BlackboardPanel(this, BlackboardPanel.Part.Character);
            Add(_board);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);

            // Ctrl+D (and Ctrl+C, Ctrl+V) on variable nodes: one more node of the same variable
            serializeGraphElements = CopyVariables;
            canPasteSerializedData = data => data != null && data.StartsWith(COPY_PREFIX);
            unserializeAndPaste = PasteVariables;
            RegisterCallback<MouseMoveEvent>(evt => _pointer = contentViewContainer.WorldToLocal(evt.mousePosition));

            // Esc inside a subgraph: back to the graph
            RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape || _scope == null)
                    return;

                CloseSubgraph();
                evt.StopPropagation();
            });

            // Frames: what is typed and dragged on the canvas goes to the asset
            groupTitleChanged = (group, _) =>
            {
                if (!_rebuilding && group is FrameGroup frame)
                    _document?.RenameFrame(frame.Data, group.title);
            };
            elementsAddedToGroup = (group, added) => KeepFrameMembers(group, added, true);
            elementsRemovedFromGroup = (group, removed) => KeepFrameMembers(group, removed, false);
        }

        // The node an arrow leaves, by the arrow: what a frame takes along with its nodes is found through it
        private readonly Dictionary<Transition, Node> _sources = new();

        private readonly BlackboardPanel _board;

        // The variable of the blackboard row being dragged (null — something else is dragged)
        private static string DraggedVariable()
        {
            if (DragAndDrop.GetGenericData("DragSelection") is not List<ISelectable> dragged)
                return null;

            foreach (ISelectable item in dragged)
            {
                if (item is BlackboardField field && BlackboardPanel.VariableOf(field) != null)
                    return BlackboardPanel.VariableOf(field);
            }

            return null;
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            string variable = DraggedVariable();
            if (variable == null || _document == null)
                return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            string variable = DraggedVariable();
            if (variable != null && _document != null && DropOnNode(variable, evt.target as VisualElement))
            {
                DragAndDrop.AcceptDrag();
                evt.StopPropagation();
                return;
            }

            if (variable == null || _document == null || !_document.HasVariableNode(variable))
                return;

            Vector2 at = contentViewContainer.WorldToLocal(evt.mousePosition);
            DragAndDrop.AcceptDrag();
            evt.StopPropagation();
            schedule.Execute(() => _document.AddVariableNodeAt(variable, at));
        }

        // A row of the blackboard dropped on a Compare node puts its variable there; a flag dropped on a Branch becomes
        // a node of the variable wired into its Condition pin
        private bool DropOnNode(string variable, VisualElement under)
        {
            if (_document.IsReadOnly)
                return false;                           // a graph opened for viewing: nothing is wired, not even in memory

            VariableType kind = _document.TypeOfVariable(variable);
            for (VisualElement element = under; element != null; element = element.parent)
            {
                // An object variable dropped on a sensor: the sensor writes what it sees into it
                if (element is SensorNode watching && kind == VariableType.Transform)
                {
                    Vector2 beside = watching.GetPosition().position + new Vector2(300f, 60f);
                    schedule.Execute(() =>
                    {
                        GraphVariableNode node = _document.VariableNodes.FirstOrDefault(found => found.Variable == variable)
                                                 ?? _document.AddVariableNodeAt(variable, beside);
                        _document.SetSensorVariable(watching.Sensor, variable, node);
                    });
                    return true;
                }

                if (element is ConditionNode { Condition: CompareCondition compare } asking)
                {
                    Vector2 beside = asking.GetPosition().position + new Vector2(-170f, 10f);
                    schedule.Execute(() =>
                    {
                        compare.Variable = variable;
                        compare.Kind = kind;
                        _document.SetReader(_document.AddVariableNodeAt(variable, beside), compare);
                        _document.Touch();
                    });
                    return true;
                }

                if (element is StateNode changing && changing.State.Tasks.Count == 1)
                {
                    AgentTask task = changing.State.Tasks[0];
                    bool fits = task is NumberTask && kind is VariableType.Float or VariableType.Int or VariableType.Double ||
                                task is SetFlagTask && kind == VariableType.Bool;
                    if (fits)
                    {
                        Vector2 beside = changing.GetPosition().position + new Vector2(-170f, 50f);
                        schedule.Execute(() =>
                        {
                            if (task is NumberTask number)
                                number.Variable = variable;
                            else if (task is SetFlagTask flag)
                                flag.Variable = variable;
                            _document.SetReader(_document.AddVariableNodeAt(variable, beside), task);
                            _document.Touch();
                        });
                        return true;
                    }
                }

                // A flag dropped on a node that asks a question (a Branch, a decision): asked through its Condition pin
                if (element is StateNode node && kind == VariableType.Bool && node.ConditionAsker() is { } asker)
                {
                    Vector2 at = node.GetPosition().position + new Vector2(-200f, 70f);
                    schedule.Execute(() =>
                    {
                        if (_document.AddConditionTo(asker, "aikit.condition.compare", at) is CompareCondition flag)
                        {
                            flag.Variable = variable;
                            flag.Kind = VariableType.Bool;
                            _document.Touch();
                        }
                    });
                    return true;
                }
            }

            return false;
        }

        private static int FrameIdOf(GraphElement element) => element switch
        {
            StateNode state => state.State.Id,
            SensorNode sensor => sensor.Sensor.Id,
            _ => 0
        };

        private void KeepFrameMembers(Group group, IEnumerable<GraphElement> elements, bool inside)
        {
            if (_rebuilding || _document == null || group is not FrameGroup frame)
                return;

            bool refused = false;
            foreach (GraphElement element in elements)
            {
                // The Brain's frame is the Brain's alone (step 90): a decision, a block, a sensor stay outside it
                if (frame.Data.Brain != 0)
                {
                    refused |= inside && FrameIdOf(element) != frame.Data.Brain;
                    continue;
                }

                _document.SetFrameMember(frame.Data, FrameIdOf(element), inside);
            }

            if (refused)
                schedule.Execute(() => Show(_document));
        }

        /// <summary>A new frame around the picked nodes (and the one that was clicked).</summary>
        private void AddFrame(GraphElement clicked)
        {
            var ids = new List<int>();
            foreach (ISelectable item in selection)
            {
                if (item is GraphElement element && FrameIdOf(element) != 0)
                    ids.Add(FrameIdOf(element));
            }

            int own = FrameIdOf(clicked);
            if (own != 0 && !ids.Contains(own))
                ids.Add(own);

            schedule.Execute(() => _document.AddFrame("Comment", ids));
        }

        // ---------- subgraphs: parts of the graph collapsed into one node (15.11.2026; a look of the editor only) ----------

        // The subgraph the canvas shows now (null — the graph itself), the views of the subgraphs on the graph, the In and
        // Out nodes inside one, the pins at its edge by what crosses it, and the ids of the nodes shown
        private GraphSubgraph _scope;
        private readonly Dictionary<int, SubgraphView> _subgraphViews = new();
        private BoundaryView _boundaryIn;
        private BoundaryView _boundaryOut;
        private readonly Dictionary<string, Port> _boundaryPorts = new();
        private HashSet<int> _shownIds = new();
        private VisualElement _crumbs;

        /// <summary>The subgraph the canvas shows now (null — the graph itself; tests read it).</summary>
        public GraphSubgraph Scope => _scope;

        /// <summary>The collapsed node of a subgraph on the graph (null — not drawn; tests read it).</summary>
        public Node SubgraphNodeOf(GraphSubgraph part) =>
            part != null && _subgraphViews.TryGetValue(part.Id, out SubgraphView view) ? view : null;

        // A node stands in this view: on the graph — unless it is collapsed into a subgraph; inside one — if it is of it
        private bool Shown(GraphDocument document, AgentState state) =>
            _scope != null ? _scope.Members.Contains(state.Id) : document.SubgraphOf(state.Id) == null;

        // The ids of what the shown nodes hold (their blocks, the conditions and values they read): what a variable node
        // or a value node of this view feeds
        private HashSet<int> ShownIds(GraphDocument document)
        {
            var ids = new HashSet<int>();
            foreach (StateNode node in _nodes.Values)
            {
                ids.Add(node.State.Id);
                foreach (AgentTask task in node.State.Tasks)
                    CollectIds(task, ids);
            }

            if (_scope == null)
            {
                foreach (SensorTask sensor in document.Sensors())
                    CollectIds(sensor, ids);
            }

            return ids;
        }

        private static void CollectIds(AgentNode node, HashSet<int> ids)
        {
            if (node == null || !ids.Add(node.Id) && node.Id != 0)
                return;

            foreach (GraphFields.Link link in GraphFields.LinksOf(node.GetType()))
            {
                if (link.IsObject || link.Holds(typeof(AgentState)) || link.Holds(typeof(Transition)))
                    continue;

                foreach (object held in link.Values(node))
                    CollectIds(held as AgentNode, ids);
            }
        }

        // A node of a variable stands in this view when it feeds a node of it; on the graph also one that feeds nobody yet
        // or that a sensor fills
        private bool Shows(GraphDocument document, GraphVariableNode variable, HashSet<int> shown)
        {
            foreach (int reader in variable.Readers)
            {
                if (shown.Contains(reader))
                    return true;
            }

            if (_scope != null)
                return false;

            // On the graph: a node nobody reads yet, or the node a sensor writes into (its Object wire ends there) — never
            // one read only by the nodes of a subgraph
            return variable.Readers.Count == 0 ||
                   variable.Filled && document.Sensors().Any(sensor => sensor.StoreIn == variable.Variable);
        }

        // On the graph: a node per subgraph, where it was put; inside one: the In and Out nodes at the edges of its nodes
        private void ShowSubgraphs(GraphDocument document, List<AgentState> states)
        {
            if (_scope == null)
            {
                foreach (GraphSubgraph part in document.Subgraphs)
                {
                    bool runs = states.Exists(state => part.Members.Contains(state.Id) && document.Graph.Runs(state));
                    var view = new SubgraphView(part, runs);
                    view.SetPosition(new Rect(part.Position, SubgraphView.Size));
                    _subgraphViews[part.Id] = view;
                    AddElement(view);
                    view.RegisterCallback<GeometryChangedEvent>(_ => RouteBrainWires());   // the Brain's wire follows it
                }

                return;
            }

            float left = float.MaxValue, right = float.MinValue, top = 0f;
            int count = 0;
            foreach (StateNode node in _nodes.Values)
            {
                Vector2 at = document.PositionOf(node.State);
                left = Mathf.Min(left, at.x);
                right = Mathf.Max(right, at.x);
                top += at.y;
                count++;
            }

            float middle = count > 0 ? top / count : 0f;
            if (count == 0)
                left = right = 0f;

            _boundaryIn = new BoundaryView("In", "What leads into the subgraph from the graph");
            _boundaryIn.SetPosition(new Rect(new Vector2(left - 320f, middle), BoundaryView.Size));
            AddElement(_boundaryIn);
            _boundaryOut = new BoundaryView("Out", "Where the subgraph leads on the graph");
            _boundaryOut.SetPosition(new Rect(new Vector2(right + 380f, middle), BoundaryView.Size));
            AddElement(_boundaryOut);
        }

        // The end of an arrow whose target is not shown: on the graph — the In of the subgraph it is in; inside a
        // subgraph — a pin of the Out node named after it
        private Port BoundaryInto(GraphDocument document, AgentState target)
        {
            if (_scope == null)
            {
                GraphSubgraph part = document.SubgraphOf(target.Id);
                return part != null && _subgraphViews.TryGetValue(part.Id, out SubgraphView view) ? view.In : null;
            }

            return _boundaryOut?.PinFor(_boundaryPorts, $"out:{target.Id}", $"→ {target.DisplayName}", Direction.Input);
        }

        // The start of an arrow whose node is not shown: on the graph — a pin of its subgraph named after the node and
        // its outcome; inside a subgraph — a pin of the In node named after the node it comes from
        private Port BoundaryFrom(GraphDocument document, AgentState source, Transition arrow)
        {
            if (_scope == null)
            {
                GraphSubgraph part = document.SubgraphOf(source.Id);
                if (part == null || !_subgraphViews.TryGetValue(part.Id, out SubgraphView view))
                    return null;

                string outcome = OutcomeWord(source, arrow);
                return view.PinFor(_boundaryPorts, $"{part.Id}:{source.Id}:{(int)arrow.Trigger}:{arrow.Output}",
                    $"{source.DisplayName}: {outcome}", Direction.Output);
            }

            return _boundaryIn?.PinFor(_boundaryPorts, $"in:{source.Id}", $"← {source.DisplayName}", Direction.Output);
        }

        // An arrow's outcome in the words of its pin ("Done", "Failed", "Do", "On Tick", "Output 2")
        private static string OutcomeWord(AgentState source, Transition arrow)
        {
            if (arrow.Output >= 0 && source.Random != null)
                return $"Output {arrow.Output + 1}";

            (string success, string failure, string finished) = source.HasForegroundTasks
                ? StateNode.OutcomeNames(source)
                : (null, null, null);
            return arrow.Trigger switch
            {
                TransitionTrigger.TasksSucceeded => success ?? "Success",
                TransitionTrigger.TasksFailed => failure ?? "Failure",
                TransitionTrigger.TasksFinished => finished ?? "Done",
                TransitionTrigger.Tick => "On Tick",
                _ => source.Reasoner != null ? "Do" : "at once"
            };
        }

        // An arrow across the edge of a subgraph: drawn from pin to pin, deleted like any arrow
        private void LinkAcross(Port from, Port to, Transition arrow)
        {
            if (from == null || to == null)
                return;

            _sources[arrow] = from.node;
            List<Edge> stretches = LinkWire(from, to, arrow, arrow);
            foreach (Edge edge in stretches)
                _edges.Add(new LiveEdge { Edge = edge, Transition = arrow });

            // The Brain's wire to a decision collapsed into a subgraph leaves the Brain's frame too, from a point of its own
            if (from.userData == StateNode.DecisionsPin && stretches.Count == 1 &&
                stretches[0].edgeControl is KnotEdgeControl control)
            {
                control.drawFromCap = false;
                _brainWires.Add(new BrainWire
                {
                    Edge = stretches[0], Control = control, Transition = arrow, To = to.node, Into = to
                });
            }
        }

        /// <summary>The canvas shows the subgraph's own nodes (and its In and Out); null — the graph itself.</summary>
        public void OpenSubgraph(GraphSubgraph part)
        {
            if (_document == null)
                return;

            _document.ActiveSubgraph = part;
            Show(_document);
            schedule.Execute(() => FrameAll()).ExecuteLater(30);
        }

        /// <summary>Back to the graph itself, with the subgraph's node picked.</summary>
        public void CloseSubgraph()
        {
            if (_document == null || _scope == null)
                return;

            int id = _scope.Id;
            OpenSubgraph(null);
            schedule.Execute(() =>
            {
                if (!_subgraphViews.TryGetValue(id, out SubgraphView view))
                    return;

                ClearSelection();
                AddToSelection(view);
                FrameSelection();
            }).ExecuteLater(60);
        }

        // The path at the top of the canvas inside a subgraph: "Graph › Combo" — the graph's name goes back
        private void ShowCrumbs(GraphDocument document)
        {
            if (_crumbs == null)
            {
                _crumbs = new VisualElement { name = "subgraph-crumbs" };
                _crumbs.style.position = Position.Absolute;
                _crumbs.style.left = 344;
                _crumbs.style.top = 8;
                _crumbs.style.flexDirection = FlexDirection.Row;
                _crumbs.style.alignItems = Align.Center;
                _crumbs.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.92f);
                _crumbs.style.paddingLeft = _crumbs.style.paddingRight = 6;
                _crumbs.style.paddingTop = _crumbs.style.paddingBottom = 2;
                _crumbs.style.borderTopLeftRadius = _crumbs.style.borderTopRightRadius = 4;
                _crumbs.style.borderBottomLeftRadius = _crumbs.style.borderBottomRightRadius = 4;
                Add(_crumbs);
            }

            _crumbs.Clear();
            _crumbs.style.display = _scope == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (_scope == null)
                return;

            string graph = document.Asset != null ? document.Asset.name : "Graph";
            var back = new Button(CloseSubgraph) { text = graph, tooltip = "Back to the graph (Esc)" };
            _crumbs.Add(back);
            _crumbs.Add(new Label(" › "));
            _crumbs.Add(new Label(_scope.Title));
        }

        // The picked nodes (and the one clicked) collapsed into one subgraph
        private void CollapseSelection(StateNode clicked)
        {
            var ids = selection.OfType<StateNode>().Where(node => !node.State.IsBrain).Select(node => node.State.Id).ToList();
            if (!ids.Contains(clicked.State.Id))
                ids.Add(clicked.State.Id);
            schedule.Execute(() => _document.CollapseToSubgraph(ids));
        }

        // The subgraph's title edited in place, on its node: Enter keeps it, Esc leaves it as it was
        private void RenameSubgraph(SubgraphView view)
        {
            var field = new TextField { value = view.Part.Title };
            field.style.position = Position.Absolute;
            field.style.left = 4;
            field.style.right = 4;
            field.style.top = 2;
            view.titleContainer.Add(field);
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    _document.RenameSubgraph(view.Part, field.value);
                else if (evt.keyCode != KeyCode.Escape)
                    return;

                field.RemoveFromHierarchy();
                evt.StopPropagation();
            });
            field.RegisterCallback<FocusOutEvent>(_ => field.RemoveFromHierarchy());
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
        }

        // A pin at the edge of a subgraph: it shows a wire that crosses the edge; no new wire is drawn from it
        private sealed class BoundaryPin
        {
        }

        private static Port BoundaryPort(Node owner, Direction direction, string name)
        {
            Port port = owner.InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(Transition));
            port.portName = name;
            port.userData = new BoundaryPin();
            port.tooltip = "A wire across the edge of the subgraph — to change it, expand the subgraph or open it";
            if (port.edgeConnector != null)
                port.RemoveManipulator(port.edgeConnector);
            return port;
        }

        /// <summary>
        /// A subgraph on the graph: one node — its title, how many nodes it holds, its In (what leads into it) and a pin per
        /// arrow that leaves it ("Attack: Done"). A double click opens it.
        /// </summary>
        private sealed class SubgraphView : CanvasNode
        {
            public static readonly Vector2 Size = new(240f, 90f);

            public SubgraphView(GraphSubgraph part, bool runs)
            {
                Part = part;
                title = part.Title;
                titleContainer.style.backgroundColor = new Color(0.22f, 0.3f, 0.45f);
                capabilities &= ~(Capabilities.Renamable | Capabilities.Collapsible | Capabilities.Copiable);
                tooltip = "A subgraph: double click — open it";
                var count = new Label($"{part.Members.Count} nodes") { pickingMode = PickingMode.Ignore };
                count.style.color = new Color(0.75f, 0.75f, 0.75f);
                count.style.fontSize = 10;
                count.style.marginRight = 6;
                titleContainer.Add(count);
                In = BoundaryPort(this, Direction.Input, "In");
                inputContainer.Add(In);
                if (!runs)
                    style.opacity = 0.45f;
                RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.clickCount != 2 || evt.button != 0)
                        return;

                    GetFirstAncestorOfType<GraphViewCanvas>()?.OpenSubgraph(Part);
                    evt.StopPropagation();
                });
                RefreshPorts();
                RefreshExpandedState();
            }

            public GraphSubgraph Part { get; }

            public Port In { get; }

            private Label _running;
            private string _runningText;

            /// <summary>In Play: what runs inside it now and how long ("▶ Patrol Area 1.2 s"); null — nothing of it runs.</summary>
            public void ShowRunning(string node, float seconds)
            {
                string text = node == null ? "" : $"▶ {node} {Mathf.Floor(seconds * 10f) / 10f:0.0} s";
                if (text == _runningText)
                    return;

                _runningText = text;
                if (_running == null)
                {
                    _running = new Label { pickingMode = PickingMode.Ignore };
                    _running.style.color = ActiveColor;
                    _running.style.paddingLeft = 6;
                    _running.style.paddingBottom = 2;
                    extensionContainer.Add(_running);
                    RefreshExpandedState();
                }

                _running.text = text;
                _running.style.display = node == null ? DisplayStyle.None : DisplayStyle.Flex;
            }

            public Port PinFor(Dictionary<string, Port> pins, string key, string name, Direction direction)
            {
                if (pins.TryGetValue(key, out Port pin))
                    return pin;

                pin = BoundaryPort(this, direction, name);
                (direction == Direction.Input ? inputContainer : outputContainer).Add(pin);
                pins[key] = pin;
                RefreshPorts();
                return pin;
            }
        }

        /// <summary>The In or Out node inside a subgraph: a pin per node of the graph the subgraph is wired to.</summary>
        private sealed class BoundaryView : CanvasNode
        {
            public static readonly Vector2 Size = new(200f, 60f);

            public BoundaryView(string name, string hint)
            {
                title = name;
                tooltip = hint;
                titleContainer.style.backgroundColor = new Color(0.22f, 0.3f, 0.45f);
                capabilities &= ~(Capabilities.Renamable | Capabilities.Collapsible | Capabilities.Copiable |
                                  Capabilities.Deletable);
                RefreshExpandedState();
            }

            public Port PinFor(Dictionary<string, Port> pins, string key, string name, Direction direction)
            {
                if (pins.TryGetValue(key, out Port pin))
                    return pin;

                pin = BoundaryPort(this, direction, name);
                (direction == Direction.Input ? inputContainer : outputContainer).Add(pin);
                pins[key] = pin;
                RefreshPorts();
                return pin;
            }
        }

        // The frames of the asset, drawn over the finished canvas. A frame holds its nodes by id; what belongs to
        // those nodes comes along without being kept anywhere: the copies of variables they read, the link labels
        // and the bend points of the arrows that leave them
        private void ShowFrames(GraphDocument document)
        {
            document.EnsureBrainFrames();
            var taken = new HashSet<GraphElement>();    // an element is in one frame only: the first that claims it
            foreach (GraphFrame data in document.Frames)
            {
                // A Brain's frame holds its Brain (and what feeds the Brain: the copies of the variables it reads)
                _nodes.TryGetValue(data.Brain, out StateNode brain);
                StateNode enter = data.Brain != 0 ? brain : null;
                var members = new List<GraphElement>();
                var states = new HashSet<AgentState>();
                var sensors = new HashSet<SensorTask>();
                var tasks = new HashSet<AgentTask>();
                if (enter != null)
                {
                    members.Add(enter);
                    states.Add(enter.State);
                    foreach (AgentTask task in enter.State.Tasks)
                        tasks.Add(task);
                }

                foreach (int id in data.Members)
                {
                    if (_nodes.TryGetValue(id, out StateNode node))
                    {
                        if (node.State.IsBrain)
                            continue;                   // a brain belongs to its own frame only

                        members.Add(node);
                        states.Add(node.State);
                        foreach (AgentTask task in node.State.Tasks)
                            tasks.Add(task);
                        continue;
                    }

                    foreach (KeyValuePair<SensorTask, SensorNode> pair in _sensors)
                    {
                        if (pair.Key.Id != id)
                            continue;

                        members.Add(pair.Value);
                        sensors.Add(pair.Key);
                    }
                }

                if (members.Count == 0)
                    continue;                           // its nodes are gone: nothing to draw around

                var memberNodes = new HashSet<GraphElement>(members);
                foreach (KeyValuePair<GraphVariableNode, VariableNodeView> pair in _variables)
                {
                    bool read = false;
                    foreach (AgentTask task in tasks)
                        read |= pair.Key.Readers.Contains(task.Id);

                    bool filled = false;
                    foreach (SensorTask sensor in sensors)
                        filled |= pair.Key.Filled && sensor.StoreIn == pair.Key.Variable;

                    if (read || filled)
                        members.Add(pair.Value);
                }

                foreach (GraphElement element in graphElements.ToList())
                {
                    if (element is ConditionNode asked && _conditionOwners.TryGetValue(asked, out StateNode asker) &&
                        (memberNodes.Contains(asker) || asker == enter))
                    {
                        members.Add(element);
                        continue;
                    }

                    // A link label docked at its pin follows the pin, not the frame: held by a frame too, the frame moved it and the
                    // canvas put it back under the pin at once — the frame grew under it on every move (16.11.2026, the user's
                    // word: "a frame dragged grows down without end"); a label dragged away is the frame's
                    object wire = element switch
                    {
                        LinkNode { IsDocked: true } => null,
                        LinkNode label => label.Transition,
                        WirePoint point => point.Wire,
                        _ => null
                    };

                    // The Brain's wires leave its frame: their bend points and labels stay outside it
                    if (enter != null && wire is Transition fromBrain && enter.State.Transitions.Contains(fromBrain))
                        continue;

                    bool belongs = wire switch
                    {
                        Transition arrow => _sources.TryGetValue(arrow, out Node source) && memberNodes.Contains(source),
                        SensorTask sensor => sensors.Contains(sensor),
                        AgentTask task => tasks.Contains(task),
                        _ => false
                    };
                    if (belongs)
                        members.Add(element);
                }

                members.RemoveAll(member => !taken.Add(member));
                if (members.Count == 0)
                    continue;

                var frame = new FrameGroup(data, enter);
                AddElement(frame);
                frame.AddElements(members);
                if (enter != null)
                    WatchBrainGeometry(frame);
            }
        }

        /// <summary>
        /// A frame on the canvas: a titled box that keeps around its nodes and is dragged with them. The Brain's frame is
        /// the Brain's alone: its wires leave it each from a point of its own (C10), and a new wire can be pulled from
        /// any point of its rim.
        /// </summary>
        private sealed class FrameGroup : Group
        {
            /// <summary>How near the frame's edge a press starts a new wire of the Brain, in pixels.</summary>
            public const float RIM = 10f;

            public FrameGroup(GraphFrame data, StateNode brain)
            {
                Data = data;
                Brain = brain;
                title = data.Title;
                tooltip = brain != null
                    ? "The Brain's frame: its wires leave it each from a point of its own. Pull a new wire from its rim " +
                      "to a decision, or let a decision's wire go over it"
                    : "Double click the title to rename. Drag a node in to add it; Shift + drag takes it out";
                if (brain != null)
                    RegisterCallback<MouseDownEvent>(OnRimDown, TrickleDown.TrickleDown);
            }

            public GraphFrame Data { get; }

            /// <summary>The Brain this frame is drawn round (null — a plain frame, a comment).</summary>
            public StateNode Brain { get; }

            // A press on the rim of the Brain's frame pulls a new wire from the Brain's Decisions pin, leaving from there
            private void OnRimDown(MouseDownEvent evt)
            {
                if (evt.button != 0 || Brain?.Decisions == null)
                    return;

                GraphViewCanvas canvas = GetFirstAncestorOfType<GraphViewCanvas>();
                if (canvas == null)
                    return;

                Vector2 at = canvas.contentViewContainer.WorldToLocal(evt.mousePosition);
                Rect box = GetPosition();
                if (!GraphLayout.OnRim(box, at, RIM))
                    return;

                // The top is the frame's title: pressed there, the frame is dragged as ever
                FrameSide side = GraphLayout.SideOf(box, at, out float along);
                if (side == FrameSide.Top)
                    return;

                if (canvas.StartBrainWire(Brain, side, along, evt))
                    evt.StopPropagation();
            }
        }

        // What is copied: the picked variable nodes (nothing else is copied)
        private string CopyVariables(IEnumerable<GraphElement> elements) =>
            _document == null ? COPY_PREFIX : CopyData(_document, elements.OfType<VariableNodeView>().Select(view => view.Data));

        // What a copy keeps of each variable node: its variable and where it stood (never its place in a list — a paste
        // into another graph, or after the list changed, would take another variable's node)
        [Serializable]
        private sealed class CopiedVariables
        {
            public List<string> Variables = new();
            public List<Vector2> Places = new();
        }

        /// <summary>The text Ctrl+C keeps for these variable nodes of the document (NodesToPaste reads it back).</summary>
        public static string CopyData(GraphDocument document, IEnumerable<GraphVariableNode> nodes)
        {
            var copied = new CopiedVariables();
            foreach (GraphVariableNode node in nodes)
            {
                if (node == null || document == null || !document.VariableNodes.Contains(node))
                    continue;

                copied.Variables.Add(node.Variable);
                copied.Places.Add(node.Position);
            }

            return COPY_PREFIX + JsonUtility.ToJson(copied);
        }

        /// <summary>
        /// The variable nodes of the document a paste of this text duplicates: for each copied node, the node of the same
        /// variable where it stood (else the first node of that variable); a variable the document has no node of — nothing
        /// (a paste never duplicates another variable).
        /// </summary>
        public static List<GraphVariableNode> NodesToPaste(GraphDocument document, string data)
        {
            var originals = new List<GraphVariableNode>();
            if (document == null || data == null || !data.StartsWith(COPY_PREFIX))
                return originals;

            CopiedVariables copied;
            try
            {
                copied = JsonUtility.FromJson<CopiedVariables>(data.Substring(COPY_PREFIX.Length));
            }
            catch (ArgumentException)
            {
                return originals;                       // a copy of another version: nothing to paste
            }

            for (int i = 0; copied != null && i < copied.Variables.Count; i++)
            {
                string variable = copied.Variables[i];
                Vector2 place = i < copied.Places.Count ? copied.Places[i] : Vector2.zero;
                GraphVariableNode original =
                    document.VariableNodes.FirstOrDefault(node => node.Variable == variable && node.Position == place) ??
                    document.VariableNodes.FirstOrDefault(node => node.Variable == variable);
                if (original != null)
                    originals.Add(original);
            }

            return originals;
        }

        // Where the pointer was last over the canvas (the canvas's own coordinates): a paste goes there
        private Vector2? _pointer;

        private void PasteVariables(string operation, string data)
        {
            List<GraphVariableNode> originals = NodesToPaste(_document, data);
            if (originals.Count == 0)
                return;

            List<Vector2> places = PastePlaces(originals.Select(original => original.Position).ToList(), _pointer);
            schedule.Execute(() =>
            {
                for (int i = 0; i < originals.Count; i++)
                    _document.DuplicateVariableNode(originals[i], places[i]);
            });
        }

        /// <summary>
        /// Where pasted nodes go: the first where the pointer is, the others where they stood from it (the copied group
        /// keeps its shape). No pointer over the canvas yet — next to the originals, as before.
        /// </summary>
        public static List<Vector2> PastePlaces(IReadOnlyList<Vector2> copied, Vector2? pointer)
        {
            var places = new List<Vector2>();
            if (copied == null || copied.Count == 0)
                return places;

            Vector2 shift = pointer.HasValue ? pointer.Value - copied[0] : new Vector2(40f, 50f);
            foreach (Vector2 place in copied)
                places.Add(place + shift);
            return places;
        }

        private Node _pointed;
        private StyleColor _pointedColor;

        /// <summary>Marks the node a link points at (its title takes the colour of links); null — nothing is marked.</summary>
        private void Point(Node node)
        {
            if (_pointed == node)
                return;

            if (_pointed != null)
                _pointed.titleContainer.style.backgroundColor = _pointedColor;

            _pointed = node;
            if (node == null)
                return;

            _pointedColor = node.titleContainer.style.backgroundColor;
            node.titleContainer.style.backgroundColor = PointedColor;
        }

        /// <summary>Selects the node and brings it into view.</summary>
        private void Reveal(Node node)
        {
            if (node == null)
                return;

            ClearSelection();
            AddToSelection(node);
            FrameSelection();
        }

        private static string NameOf(Node node) => node is StateNode state ? state.State.DisplayName : node != null ? node.title : "?";

        // A double click on a wire puts a point on it there: the wire can be laid out around the nodes
        private void OnMouseDown(MouseDownEvent evt)
        {
            if (evt.clickCount != 2 || evt.button != 0 || _document == null)
                return;

            Edge edge = null;
            for (var element = evt.target as VisualElement; element != null && edge == null; element = element.parent)
                edge = element as Edge;

            if (edge == null || !_segments.TryGetValue(edge, out (object Wire, int Index) segment))
                return;

            Vector2 at = contentViewContainer.WorldToLocal(evt.mousePosition) - WirePoint.Size * 0.5f;
            evt.StopPropagation();
            schedule.Execute(() => _document.AddWaypoint(segment.Wire, segment.Index, at));
        }

        // Draws a wire through its points: a small node per point, an edge per stretch. Every stretch carries the same
        // data as the whole wire, so deleting any of them deletes the wire
        private List<Edge> LinkWire(Port from, Port to, object data, object wire)
        {
            var stretches = new List<Edge>();
            IReadOnlyList<Vector2> points = _document.WaypointsOf(wire);
            Port previous = from;
            for (int i = 0; i < points.Count; i++)
            {
                var point = new WirePoint(wire, i, from.portType, from.portColor);
                point.SetPosition(new Rect(points[i], WirePoint.Size));
                AddElement(point);
                Edge stretch = Link(previous, point.In, data, dashed: false);
                _segments[stretch] = (wire, i);
                stretches.Add(stretch);
                previous = point.Out;
            }

            Edge last = Link(previous, to, data, dashed: false);
            _segments[last] = (wire, points.Count);
            stretches.Add(last);
            return stretches;
        }

        public VisualElement Element => this;

        public event Action<object> Selected;

        public GraphDocument Document => _document;

        /// <summary>The graph is flat: a new node goes into the graph's list, whatever is selected (null — the root).</summary>
        public AgentState ParentForNewState => null;

        public IReadOnlyList<AgentState> SelectedStates =>
            selection.OfType<StateNode>().Select(node => node.State).ToList();

        public object SelectedElement
        {
            get
            {
                ISelectable first = selection.FirstOrDefault();
                return first switch
                {
                    StateNode node => node.State,
                    Edge edge => edge.userData,
                    _ => null
                };
            }
        }

        public Vector2 ToGraph(Vector2 screenPosition)
        {
            EditorWindow window = EditorWindow.focusedWindow;
            Vector2 local = window != null ? screenPosition - window.position.position : screenPosition;
            return contentViewContainer.WorldToLocal(local);
        }

        public void Show(GraphDocument document)
        {
            _document = document;
            _rebuilding = true;
            try
            {
                Build(document);
            }
            finally
            {
                _rebuilding = false;                    // a graph that cannot be drawn whole never leaves the canvas deaf
            }
        }

        private void Build(GraphDocument document)
        {
            DeleteElements(graphElements.ToList());
            _nodes.Clear();
            _variables.Clear();
            _segments.Clear();
            _sensors.Clear();
            _edges.Clear();
            _sources.Clear();
            _docked.Clear();
            _marked.Clear();
            _brainWires.Clear();
            _valueViews.Clear();
            ClearHandles();
            _givenBy.Clear();
            _leafId = 0;
            _pointed = null;

            AgentState root = document.Graph.Root;
            List<AgentState> states = document.States();
            GraphSubgraph scope = document.ActiveSubgraph;
            _scope = scope;
            _subgraphViews.Clear();
            _boundaryIn = null;
            _boundaryOut = null;
            _boundaryPorts.Clear();

            // The graph is flat (the canvas is the program): the root is not drawn — its sensors are, as sensor nodes
            foreach (AgentState state in states)
            {
                if (state == root || !Shown(document, state))
                    continue;                           // collapsed into a subgraph (or, inside one, not of it)

                var node = new StateNode(document, state, false, false);
                _nodes[state.Id] = node;

                // A node that never runs (no Brain, a decision not wired to it, a block no chain reaches) is drawn faded:
                // the canvas is the program, and this part of it is not
                if (!state.IsBrain && !document.Graph.Runs(state))
                {
                    node.style.opacity = 0.45f;
                    node.tooltip = document.Graph.Brain == null
                        ? "Never runs: the graph has no Brain"
                        : "Never runs: no decision's chain leads here";
                }
                // The deciding brain is a node like any other: it stands where it is put inside its frame, so what is
                // wired into its rules can be laid out around it
                node.SetPosition(new Rect(document.PositionOf(state), new Vector2(300f, 140f)));
                AddElement(node);
            }

            // The graph starts in its Brain: no Start marker — the Brain has no input pin, it is the start itself
            ShowSubgraphs(document, states);
            foreach (AgentState state in states)
            {
                _nodes.TryGetValue(state.Id, out StateNode node);
                foreach (Transition transition in state.Transitions)
                {
                    AgentState target = transition.Target;
                    bool shownTarget = target != null && _nodes.ContainsKey(target.Id);
                    if (node != null && (shownTarget || target == null))
                        LinkTransition(node.PortFor(transition), transition);
                    else if (node != null && target != null)
                        LinkAcross(node.PortFor(transition), BoundaryInto(document, target), transition);
                    else if (node == null && shownTarget && state != root)
                        LinkAcross(BoundaryFrom(document, state, transition), _nodes[target.Id].In, transition);
                }
            }

            foreach (SensorTask sensor in scope == null ? document.Sensors() : new List<SensorTask>())
            {
                var node = new SensorNode(document, sensor);
                node.SetPosition(new Rect(document.PositionOfNode(sensor), new Vector2(240f, 110f)));
                _sensors[sensor] = node;
                AddElement(node);

                // The sensors of Detection: small nodes, each wired into its Sensors pin
                if (sensor is not DetectionTask detection)
                    continue;

                for (int i = 0; i < detection.Sensors.Count; i++)
                {
                    SensorTask shape = detection.Sensors[i];
                    var view = new ShapeNode(document, detection, shape);
                    Vector2 place = document.PositionOfNode(shape);
                    if (place == Vector2.zero)
                        place = document.PositionOfNode(sensor) + new Vector2(-260f, i * 130f);
                    view.SetPosition(new Rect(place, ShapeNode.Size));
                    AddElement(view);
                    Link(view.Out, node.SensorsPort, new ShapeWire { Detection = detection, Shape = shape }, dashed: false);
                }
            }

            // Variables: what a sensor sees is a variable, drawn as small nodes. The sensor points an arrow at one node
            // of its variable; a node that reads the variable gets its wire from the node the wire was drawn from — a
            // copy of the variable put next to a node and wired to it keeps the wire short. Nothing is rewired by itself
            document.EnsureVariableNodes();
            HashSet<int> shown = ShownIds(document);
            _shownIds = shown;
            foreach (GraphVariableNode variableNode in document.VariableNodes)
            {
                if (!Shows(document, variableNode, shown))
                    continue;                           // it feeds only nodes of another view (a subgraph, or the graph)

                var view = new VariableNodeView(variableNode, ValueFields.PinType(document.TypeOfVariable(variableNode.Variable)));
                view.SetPosition(new Rect(variableNode.Position, VariableNodeView.Size));
                _variables[variableNode] = view;
                AddElement(view);
            }

            foreach (KeyValuePair<SensorTask, SensorNode> pair in _sensors)
            {
                GraphVariableNode nearest = document.SourceNode(pair.Key.StoreIn);
                if (nearest != null && _variables.TryGetValue(nearest, out VariableNodeView filled))
                    LinkWire(pair.Value.ObjectPort, filled.In, null, pair.Key);
            }

            foreach (StateNode node in _nodes.Values)
            {
                foreach (Port port in node.DataPorts)
                {
                    var link = (DataLink)port.userData;
                    string variable = ValueFields.VariableOf(link.Owner, link.Field);
                    // Only a wire somebody drew is shown: a field that names its variable needs no line across the canvas
                    GraphVariableNode nearest = string.IsNullOrEmpty(variable)
                        ? null
                        : document.WiredNode(link.Owner, variable);
                    if (nearest != null && _variables.TryGetValue(nearest, out VariableNodeView feeder))
                        LinkWire(feeder.Out, port, link, link.Owner);
                }
            }

            // The objects a shape of Detection works with (the Zone's zone), each by its wire
            foreach (ShapeNode shapeView in graphElements.OfType<ShapeNode>().ToList())
                LinkVariables(document, shapeView.DataPorts);

            ShowConditions(document);
            ShowValues(document);
            MarkNodes(document);
            if (scope == null)
                ShowFrames(document);
            ShowCrumbs(document);
            foreach (StateNode node in _nodes.Values)
            {
                node.ApplyFold();                       // the wires are drawn: what leads nowhere may fold away

                // Laid out (or moved, folded, grown): its docked link labels stand at its pins again, the Brain's wires
                // find their points on the frame again
                node.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    DockLinks();
                    RouteBrainWires();
                });
            }

            if (_brainWires.Count > 0 && !_document.Frames.Any(frame => frame.Brain != 0 && _nodes.ContainsKey(frame.Brain)))
                RouteBrainWires();                      // no frame: the wires leave the Brain's own box
            PlaceJustAdded(document);
            SpreadOutOnOpen(document);
            _board.Refresh(document);
            VariablesWindow.Follow(document);           // the graph's variables: in their own window
        }

        /// <summary>Pixels of the canvas kept free between a new node and the ones it would land on.</summary>
        private const float NODE_GAP = 24f;

        // The new nodes the canvas has taken from the document and not placed yet: kept across drawings, so a node an edit
        // made is placed even when the next edit draws the canvas anew before the first layout (a condition picked with
        // nothing selected: its Branch, then the condition)
        private readonly List<object> _toPlace = new();

        // The nodes the last edits made land on nothing (the project's rule): once each one's size is known — its first
        // layout — it slides down past every node it would cover, and its place is kept
        private void PlaceJustAdded(GraphDocument document)
        {
            foreach (object made in document.Waiting)
            {
                if (!_toPlace.Contains(made))
                    _toPlace.Add(made);
            }

            document.Placed();
            foreach (object added in _toPlace.ToList())
            {
                GraphElement view = added switch
                {
                    AgentState state when _nodes.TryGetValue(state.Id, out StateNode node) => node,
                    GraphVariableNode variable when _variables.TryGetValue(variable, out VariableNodeView node) => node,
                    SensorTask sensor when _sensors.TryGetValue(sensor, out SensorNode node) => node,
                    SensorTask shape => graphElements.OfType<ShapeNode>().FirstOrDefault(node => node.Shape == shape),
                    AgentCondition condition => _conditionOwners.Keys.FirstOrDefault(node => node.Condition == condition),
                    AgentValue value when _valueViews.TryGetValue(value, out ValueNodeView node) => node,
                    _ => null
                };
                if (view == null)
                {
                    _toPlace.Remove(added);             // not on the canvas any more: nothing to place
                    continue;
                }

                EventCallback<GeometryChangedEvent> once = null;
                once = _ =>
                {
                    view.UnregisterCallback(once);
                    if (view.panel == null || !_toPlace.Contains(added))
                        return;                         // drawn anew meanwhile: its new node is placed instead

                    _toPlace.Remove(added);
                    PlaceFree(view);
                };
                view.RegisterCallback(once);
            }
        }

        // Slides the node down past every node it covers; the new place goes to the document
        private void PlaceFree(GraphElement view)
        {
            if (_document == null || view.panel == null)
                return;

            Rect own = view.GetPosition();
            var taken = new List<Rect>();
            foreach (Node other in graphElements.OfType<Node>())
            {
                if (other != view && other.parent is not null && !(view is Node && other.Contains(view)))
                    taken.Add(other.GetPosition());
            }

            Vector2 free = GraphLayout.FreeSpot(own, taken, NODE_GAP);
            if ((free - own.position).sqrMagnitude < 0.01f)
                return;

            view.SetPosition(new Rect(free, own.size));
            Keep(view, free);
        }

        // The graphs and subgraphs already spread out on this canvas (the asset and the subgraph id; 0 — the graph)
        private readonly HashSet<(BehaviorGraphAsset, int)> _spreadOut = new();

        // Rounds of waiting for the nodes' first layout before spreading them out
        private const int SPREAD_TRIES = 20;

        /// <summary>
        /// The first time a graph or a subgraph is shown, nodes that lie on each other are slid apart (the project's rule:
        /// nodes never land on each other; 16.11.2026, the user's word "when building the graphs don't lay nodes on each
        /// other") — top to bottom, each one past the ones above it, once their sizes are known.
        /// </summary>
        private void SpreadOutOnOpen(GraphDocument document)
        {
            if (document.IsReadOnly || document.Asset == null)
                return;

            var key = (document.Asset, _scope?.Id ?? 0);
            if (!_spreadOut.Add(key))
                return;

            int tries = 0;
            IVisualElementScheduledItem item = null;
            item = schedule.Execute(() =>
            {
                if (_document != document || (_scope?.Id ?? 0) != key.Item2)
                {
                    item.Pause();                       // another graph or view meanwhile
                    return;
                }

                List<Node> nodes = Spreadable().ToList();
                if (nodes.Any(node => node.layout.width < 1f) && ++tries < SPREAD_TRIES)
                    return;                             // not laid out yet

                item.Pause();
                SpreadOut(nodes);
            }).Every(50);
        }

        /// <summary>Slides apart the nodes of the shown view that lie on each other; how many were moved.</summary>
        public int SpreadOut() => SpreadOut(Spreadable().ToList());

        private int SpreadOut(List<Node> nodes)
        {
            var placed = new List<Rect>();
            int moved = 0;
            foreach (Node node in nodes.OrderBy(node => node.GetPosition().y).ThenBy(node => node.GetPosition().x))
            {
                Rect own = node.GetPosition();
                Vector2 free = GraphLayout.FreeSpot(own, placed, NODE_GAP);
                if ((free - own.position).sqrMagnitude > 0.01f)
                {
                    node.SetPosition(new Rect(free, own.size));
                    Keep(node, free);
                    moved++;
                }

                placed.Add(new Rect(free, own.size));
            }

            return moved;
        }

        // The nodes a spreading may move: not the labels docked at pins, the bend points or the In / Out of a subgraph
        private IEnumerable<Node> Spreadable() =>
            graphElements.OfType<Node>().Where(node => node.parent != null &&
                                                        node is not LinkNode and not WirePoint and not BoundaryView);

        // The node's new place goes to the document
        private void Keep(GraphElement view, Vector2 free)
        {
            switch (view)
            {
                case StateNode node:
                    _document.Move(node.State, free);
                    break;
                case VariableNodeView node:
                    _document.MoveVariableNode(node.Data, free);
                    break;
                case SensorNode node:
                    _document.MoveNode(node.Sensor, free);
                    break;
                case ShapeNode node:
                    _document.MoveNode(node.Shape, free);
                    break;
                case ConditionNode node:
                    _document.MoveNode(node.Condition, free);
                    break;
                case ValueNodeView node:
                    _document.MoveNode(node.Value, free);
                    break;
                case SubgraphView node:
                    _document.MoveSubgraph(node.Part, free);
                    break;
            }
        }

        private void LinkTransition(Port from, Transition transition)
        {
            AgentState target = transition.Target ?? _document.Graph.Root;
            if (from == null || !_nodes.TryGetValue(target.Id, out StateNode to))
                return;                                 // to the root, and the flat view does not draw it

            _sources[transition] = from.node;

            // An arrow shown as a link is not drawn all the way: it ends at a label that names the node it leads to —
            // standing at its pin (docked, stacked under the other labels of the pin), or where it was dragged
            Port end = to.In;
            if (_document.TryLinkOf(transition, out Vector2 linkAt))
            {
                bool docked = _document.IsDocked(transition);
                var label = new LinkNode(transition, target.DisplayName, from.portColor, docked, () => GoTo(target));
                if (docked)
                {
                    if (!_docked.TryGetValue(from, out List<LinkNode> stack))
                        _docked[from] = stack = new List<LinkNode>();
                    label.SetPosition(new Rect(GuessSpot(from, stack.Count), label.Size));
                    stack.Add(label);
                }
                else
                {
                    label.SetPosition(new Rect(linkAt, label.Size));
                }

                AddElement(label);
                end = label.In;

                // And a mark at the other end, at the input of the node the arrow leads to: where it comes from (one per
                // node it comes from, however many of its arrows come)
                Node source = from.node;
                if (_marked.Add((to, source)))
                    to.AddIncoming($"← {NameOf(source)}", () => Point(source), () => Reveal(source));
            }

            List<Edge> stretches = LinkWire(from, end, transition, transition);
            foreach (Edge edge in stretches)
                _edges.Add(new LiveEdge { Edge = edge, Transition = transition });

            // A wire of the Brain with no bend points leaves the Brain's frame from a point of its own (C10)
            if (from.userData == StateNode.DecisionsPin && end == to.In && stretches.Count == 1 &&
                stretches[0].edgeControl is KnotEdgeControl control)
            {
                control.drawFromCap = false;
                _brainWires.Add(new BrainWire
                {
                    Edge = stretches[0], Control = control, Transition = transition, To = to, Into = to.In
                });
            }
        }

        // The labels docked at each output pin, in the order of the arrows (each stands under the one before)
        private readonly Dictionary<Port, List<LinkNode>> _docked = new();

        // The "← source" marks already put on a node (target, source)
        private readonly HashSet<(Node, Node)> _marked = new();

        // Where a docked label stands before its node is laid out (the node's place and width as far as they are known)
        private Vector2 GuessSpot(Port pin, int index)
        {
            // Not laid out yet: where the document puts the node, and the width a node of the canvas starts with
            Vector2 node = pin.node is StateNode state ? _document.PositionOf(state.State) : Vector2.zero;
            int row = pin.parent != null ? Mathf.Max(0, pin.parent.IndexOf(pin)) : 0;
            var guess = new Rect(node.x + 300f - 8f, node.y + 40f + 24f * row, 16f, LinkNode.HEIGHT);
            return GraphLayout.LinkSpot(guess, index, LinkNode.HEIGHT);
        }

        // ---------- the Brain's wires: each leaves the Brain's frame from a point of its own (C10) ----------

        /// <summary>Pixels between the lanes of the Brain's wires (at most: they share the room before the decisions).</summary>
        private const float LANE_SPACING = 14f;

        private sealed class BrainWire
        {
            public Edge Edge;
            public KnotEdgeControl Control;
            public Transition Transition;

            /// <summary>The node the wire leads to: a decision, or the node of the subgraph a decision is collapsed into.</summary>
            public Node To;

            /// <summary>The pin it comes into (the decision's In, the subgraph's In).</summary>
            public Port Into;
        }

        private readonly List<BrainWire> _brainWires = new();

        // A wire pulled from the rim of the Brain's frame: where it leaves, given to it once it is made
        private (FrameSide Side, float Along)? _pendingExit;
        private EdgeDragHelper _rimDrag;
        private VisualElement _rimCapture;

        private void WatchBrainGeometry(FrameGroup frame) =>
            frame.RegisterCallback<GeometryChangedEvent>(_ => RouteBrainWires());

        /// <summary>
        /// The Brain's wires find their points on its frame (its own box when it has no frame) and their lanes again — once
        /// the frame and the decisions are laid out, and whenever one of them moves or changes size.
        /// </summary>
        private void RouteBrainWires()
        {
            if (_brainWires.Count == 0 || _document == null)
                return;

            foreach (IGrouping<Node, BrainWire> ofBrain in _brainWires.GroupBy(wire =>
                         _sources.TryGetValue(wire.Transition, out Node brain) ? brain : null))
            {
                if (ofBrain.Key is not StateNode brain)
                    continue;

                FrameGroup frame = graphElements.OfType<FrameGroup>().FirstOrDefault(found => found.Brain == brain);
                GraphElement box = frame != null ? frame : brain;
                Rect around = box.GetPosition();
                if (box.panel == null || around.width < 1f || around.height < 1f)
                    continue;                           // not laid out yet: the wires are drawn pin to pin meanwhile

                List<BrainWire> wires = ofBrain.ToList();
                var decisions = new List<Rect>();
                var stored = new List<(FrameSide Side, float Along)?>();
                foreach (BrainWire wire in wires)
                {
                    decisions.Add(wire.To.GetPosition());
                    if (_exitDrag != null && _exitDrag.Transition == wire.Transition)
                        stored.Add((_exitDrag.Side, _exitDrag.Along));          // its handle is being dragged
                    else
                        stored.Add(_document.TryExitOf(wire.Transition, out FrameSide side, out float along)
                            ? (side, along)
                            : null);
                }

                GraphLayout.BrainExit[] exits = GraphLayout.BrainExits(around, decisions, LANE_SPACING, stored);
                for (int i = 0; i < wires.Count; i++)
                {
                    wires[i].Control.FramePath = GraphLayout.BrainWirePath(exits[i], InPinOf(wires[i].To, wires[i].Into));
                    wires[i].Edge.UpdateEdgeControl();
                    PlaceHandle(wires[i], exits[i].Point, around);
                }
            }
        }

        // ---------- the handles of the Brain's wires on its frame (16.11.2026, the user's word) ----------

        // One circle per wire of the Brain, where it leaves the frame: dragged along the rim, the wire leaves there
        private readonly Dictionary<Transition, ExitHandle> _exitHandles = new();
        private ExitHandle _exitDrag;

        /// <summary>The handles of the Brain's wires drawn now (tests read them).</summary>
        public int ExitHandleCount => _exitHandles.Count;

        private void PlaceHandle(BrainWire wire, Vector2 point, Rect frame)
        {
            if (!_exitHandles.TryGetValue(wire.Transition, out ExitHandle handle))
            {
                handle = new ExitHandle(this, wire.Transition, wire.Edge is WireEdge tinted ? tinted.Tint : ConditionColor);
                _exitHandles[wire.Transition] = handle;
                contentViewContainer.Add(handle);
            }

            handle.Frame = frame;
            handle.style.left = point.x - ExitHandle.RADIUS;
            handle.style.top = point.y - ExitHandle.RADIUS;
        }

        private void ClearHandles()
        {
            foreach (ExitHandle handle in _exitHandles.Values)
                handle.RemoveFromHierarchy();
            _exitHandles.Clear();
            _exitDrag = null;
        }

        /// <summary>
        /// A Brain's wire is let go at a point of its frame (its handle dragged there): it leaves the frame there, kept with
        /// the graph. Tests call it as the handle does.
        /// </summary>
        public void MoveExit(Transition wire, Vector2 at)
        {
            if (_document == null || wire == null || !_exitHandles.TryGetValue(wire, out ExitHandle handle))
                return;

            FrameSide side = GraphLayout.SideOf(handle.Frame, at, out float along);
            _document.SetExit(wire, side, along);
        }

        /// <summary>A circle on the Brain's frame where a wire leaves it; dragged along the rim, the wire goes with it.</summary>
        private sealed class ExitHandle : VisualElement
        {
            public const float RADIUS = 6f;

            private readonly GraphViewCanvas _canvas;

            public ExitHandle(GraphViewCanvas canvas, Transition wire, Color color)
            {
                _canvas = canvas;
                Transition = wire;
                name = "brain-wire-handle";
                tooltip = "Drag along the frame: the wire leaves the Brain's frame here";
                style.position = Position.Absolute;
                style.width = style.height = RADIUS * 2f;
                style.borderTopLeftRadius = style.borderTopRightRadius = RADIUS;
                style.borderBottomLeftRadius = style.borderBottomRightRadius = RADIUS;
                style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
                style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 2f;
                style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = color;
                RegisterCallback<MouseDownEvent>(OnDown);
                RegisterCallback<MouseMoveEvent>(OnMove);
                RegisterCallback<MouseUpEvent>(OnUp);
            }

            public Transition Transition { get; }

            /// <summary>The frame it slides on (as it stood when the wires were last laid out).</summary>
            public Rect Frame;

            public FrameSide Side;
            public float Along;

            private void OnDown(MouseDownEvent evt)
            {
                if (evt.button != 0 || _canvas._document == null || _canvas._document.IsReadOnly)
                    return;

                this.CaptureMouse();
                _canvas._exitDrag = this;
                evt.StopPropagation();
            }

            private void OnMove(MouseMoveEvent evt)
            {
                if (_canvas._exitDrag != this)
                    return;

                Vector2 at = _canvas.contentViewContainer.WorldToLocal(evt.mousePosition);
                Side = GraphLayout.SideOf(Frame, at, out Along);
                _canvas.RouteBrainWires();
                evt.StopPropagation();
            }

            private void OnUp(MouseUpEvent evt)
            {
                if (_canvas._exitDrag != this)
                    return;

                this.ReleaseMouse();
                _canvas._exitDrag = null;
                Vector2 at = _canvas.contentViewContainer.WorldToLocal(evt.mousePosition);
                _canvas.MoveExit(Transition, at);
                evt.StopPropagation();
            }
        }

        // The middle of a node's In pin, where the canvas's content has it
        private Vector2 InPinOf(Node node, Port pin)
        {
            VisualElement connector = pin.Q("connector") ?? pin;
            if (connector.panel == null || connector.layout.width < 1f)
                return node.GetPosition().position + new Vector2(0f, GraphLayout.IN_PIN_Y);

            return connector.ChangeCoordinatesTo(contentViewContainer, connector.layout.size * 0.5f);
        }

        /// <summary>
        /// A press on the rim of the Brain's frame: a new wire is pulled from the Brain's Decisions pin, and once it is let
        /// go on a decision, it leaves the frame there (the point is kept with the graph).
        /// </summary>
        private bool StartBrainWire(StateNode brain, FrameSide side, float along, MouseDownEvent evt)
        {
            if (brain.Decisions?.edgeConnector?.edgeDragHelper is not { } helper || _document == null || _document.IsReadOnly)
                return false;

            helper.draggedPort = brain.Decisions;
            helper.edgeCandidate = new Edge();
            if (!helper.HandleMouseDown(evt))
            {
                helper.Reset();
                return false;
            }

            _pendingExit = (side, along);
            _rimDrag = helper;
            _rimCapture = evt.currentTarget as VisualElement;
            _rimCapture?.CaptureMouse();
            return true;
        }

        private void OnRimMove(MouseMoveEvent evt)
        {
            if (_rimDrag == null)
                return;

            _rimDrag.HandleMouseMove(evt);
            evt.StopPropagation();
        }

        private void OnRimUp(MouseUpEvent evt)
        {
            if (_rimDrag == null)
                return;

            EdgeDragHelper helper = _rimDrag;
            _rimDrag = null;
            _rimCapture?.ReleaseMouse();
            _rimCapture = null;
            helper.HandleMouseUp(evt);
            evt.StopPropagation();
            schedule.Execute(() => _pendingExit = null);    // used by the wire made, if any
        }

        /// <summary>
        /// A decision's In wire let go over the Brain's frame or the Brain: the decision is wired to the Brain, leaving the
        /// frame at the point it was let go (C10). An already wired decision only moves its wire to that point.
        /// </summary>
        private void WireToBrain(Port decisionIn, StateNode brain, Vector2 world)
        {
            AgentState decision = (decisionIn.node as StateNode)?.State;
            if (decision?.Reasoner == null || brain?.State == null || _document == null)
            {
                Refuse(decisionIn, brain?.Decisions ?? decisionIn);
                return;
            }

            FrameGroup frame = graphElements.OfType<FrameGroup>().FirstOrDefault(found => found.Brain == brain);
            Rect around = frame != null ? frame.GetPosition() : brain.GetPosition();
            FrameSide side = GraphLayout.SideOf(around, contentViewContainer.WorldToLocal(world), out float along);
            AgentState brainState = brain.State;
            schedule.Execute(() =>
            {
                Transition wire = brainState.Transitions.Find(found => found.Target == decision) ??
                                  _document.Connect(brainState, decision);
                _document.SetExit(wire, side, along);
            });
        }

        // Once its node is laid out, each docked label stands at its pin, under the labels before it
        private void DockLinks()
        {
            foreach (KeyValuePair<Port, List<LinkNode>> pair in _docked)
            {
                Port pin = pair.Key;
                if (pin.panel == null || pin.resolvedStyle.display == DisplayStyle.None)
                    continue;

                // The pin's connector (the round end a wire leaves from), where the canvas's content has it
                VisualElement connector = pin.Q("connector") ?? pin;
                Rect at = connector.ChangeCoordinatesTo(contentViewContainer, new Rect(Vector2.zero, connector.layout.size));
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    LinkNode label = pair.Value[i];
                    label.SetPosition(new Rect(GraphLayout.LinkSpot(at, i, LinkNode.HEIGHT), label.Size));
                }
            }
        }

        /// <summary>Selects the node of the state and brings it into view (a double click on a link label).</summary>
        private void GoTo(AgentState state)
        {
            if (state != null && _nodes.TryGetValue(state.Id, out StateNode node))
                Reveal(node);
            else if (state != null)
                FocusNode(state.Id);                    // in another view: that view opens
        }

        private Edge Link(Port from, Port to, object data, bool dashed)
        {
            // Every wire is drawn at right angles (KnotEdge): level, straight down, level — never slanted
            bool fromKnot = from.node is WirePoint;
            bool toKnot = to.node is WirePoint;
            WireEdge edge = from.ConnectTo<KnotEdge>(to);
            edge.Tint = dashed ? MarkerColor : from.portColor;      // a wire has the colour of the pin it leaves
            if (edge.edgeControl is KnotEdgeControl control)
            {
                control.FromKnot = fromKnot;
                control.ToKnot = toKnot;
                control.drawFromCap = !fromKnot;
                control.drawToCap = !toKnot;
                control.Short = to.node is LinkNode;
            }

            edge.userData = data;
            if (dashed)
                edge.capabilities &= ~Capabilities.Selectable;

            AddElement(edge);
            return edge;
        }

        public void Highlight(IReadOnlyCollection<int> activeStateIds, int leafId, float timeInLeaf, Transition last,
            float stamp)
        {
            double now = EditorApplication.timeSinceStartup;
            if (leafId != _leafId)
            {
                _leafId = leafId;
                _enterTime = now;
            }

            if (last != null && (last != _flashed || !Mathf.Approximately(stamp, _flashStamp)))
            {
                _flashed = last;
                _flashStamp = stamp;
                _flashStart = now;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin((float)(now * 6.0));
            float sinceEnter = (float)(now - _enterTime);
            bool runs = activeStateIds != null && activeStateIds.Count > 0;
            foreach (KeyValuePair<int, StateNode> pair in _nodes)
            {
                // The Brain glows while the agent runs: every decision is its choice (16.11.2026, the user: "I see no glow")
                bool active = activeStateIds != null && (activeStateIds.Contains(pair.Key) || runs && pair.Value.State.IsBrain);
                if (!active)
                {
                    pair.Value.SetIdle();
                    continue;
                }

                if (pair.Key != leafId)
                {
                    pair.Value.SetActive(ActiveDimColor, -1f);       // an ancestor on the running path
                    continue;
                }

                Color color = Color.Lerp(ActiveDimColor, ActiveColor, pulse);
                if (sinceEnter < ENTER_SECONDS)
                    color = Color.Lerp(Color.white, color, sinceEnter / ENTER_SECONDS);
                pair.Value.SetActive(color, timeInLeaf);
            }

            // A subgraph that holds the running node glows on the graph (its node stands for its nodes)
            foreach (SubgraphView part in _subgraphViews.Values)
            {
                bool running = activeStateIds != null && part.Part.Members.Exists(activeStateIds.Contains);
                Color edge = running ? Color.Lerp(ActiveDimColor, ActiveColor, pulse) : Color.clear;
                if (running && sinceEnter < ENTER_SECONDS && part.Part.Members.Contains(leafId))
                    edge = Color.Lerp(Color.white, edge, sinceEnter / ENTER_SECONDS);
                part.style.borderTopColor = part.style.borderBottomColor = edge;
                part.style.borderLeftColor = part.style.borderRightColor = edge;
                float width = running ? 3f : 0f;
                part.style.borderTopWidth = part.style.borderBottomWidth = width;
                part.style.borderLeftWidth = part.style.borderRightWidth = width;
                part.style.borderTopLeftRadius = part.style.borderTopRightRadius =
                    part.style.borderBottomLeftRadius = part.style.borderBottomRightRadius = 8f;
                part.ShowRunning(running && part.Part.Members.Contains(leafId)
                    ? _document?.States().Find(state => state.Id == leafId)?.DisplayName : null, timeInLeaf);
            }

            // The Brain's rules read the agent anew: which one wins now, what fails in the others
            foreach (StateNode node in _nodes.Values)
            {
                if (node.State.IsBrain || node.State.Reasoner != null)
                    node.RefreshLive();
            }

            float flash =Mathf.Clamp01((float)(now - _flashStart) / FLASH_SECONDS);
            foreach (LiveEdge live in _edges)
            {
                bool lit = last != null && flash < 1f && live.Transition == _flashed;
                // The wire into what runs now glows as long as it runs: the Brain's wire to the decision taken (into its
                // subgraph on the graph, from the In inside it), a wire into the running block
                bool held = false;
                if (!lit && activeStateIds != null && live.Transition?.Target != null &&
                    activeStateIds.Contains(live.Transition.Target.Id) &&
                    _sources.TryGetValue(live.Transition, out Node source))
                    held = source is BoundaryView or SubgraphView ||
                           source is StateNode from && (from.State.IsBrain || activeStateIds.Contains(from.State.Id));
                if (!lit && !held && !live.Lit)
                    continue;

                if (live.Edge is WireEdge wire)
                {
                    if (lit)
                        wire.SetFlash(Color.Lerp(FlashColor, wire.Tint, flash), Mathf.RoundToInt(Mathf.Lerp(6f, 2f, flash)));
                    else if (held)
                        wire.SetFlash(Color.Lerp(ActiveDimColor, ActiveColor, pulse), 3);
                    else
                        wire.ClearFlash();
                }

                live.Lit = lit || held;
            }
        }

        public void Frame() => FrameAll();

        // A red dot on a node with a breakpoint; a "!" on a node the graph check finds wrong, with what is wrong
        private void MarkNodes(GraphDocument document)
        {
            foreach (KeyValuePair<int, StateNode> pair in _nodes)
            {
                if (GraphBreakpoints.Has(document.Asset, pair.Key))
                {
                    var dot = new Label("\u25cf") { tooltip = "Breakpoint: Play pauses when the character comes into this node" };
                    dot.style.color = new Color(0.95f, 0.25f, 0.25f);
                    dot.style.unityTextAlign = TextAnchor.MiddleCenter;
                    pair.Value.titleContainer.Insert(0, dot);
                }

                // A problem of a task is the problem of its node: the task's own record id leads to the state that holds it
                MarkProblems(pair.Value, document.Issues
                    .Where(issue => issue.RecordId == pair.Key || OwnsNode(pair.Value.State, issue.RecordId)).ToList());
            }

            // The nodes that are not states say their problems too (a key of a condition nobody has — step 90)
            foreach (ConditionNode node in _conditionOwners.Keys)
                MarkProblems(node, document.Issues.Where(issue => issue.RecordId == node.Condition.Id).ToList());
            foreach (ValueNodeView node in _valueViews.Values)
                MarkProblems(node, document.Issues.Where(issue => issue.RecordId == node.Value.Id).ToList());
            foreach (SensorNode node in _sensors.Values)
                MarkProblems(node, document.Issues.Where(issue => issue.RecordId == node.Sensor.Id).ToList());
            foreach (ShapeNode node in graphElements.OfType<ShapeNode>().ToList())
                MarkProblems(node, document.Issues.Where(issue => issue.RecordId == node.Shape.Id).ToList());
        }

        // A "!" in the node's title, the problems in its tooltip: red for an error, yellow for a warning
        private static void MarkProblems(Node view, List<GraphIssue> wrong)
        {
            if (wrong.Count == 0)
                return;

            var mark = new Label("!") { tooltip = string.Join("\n", wrong.Select(issue => issue.Message)) };
            mark.style.color = wrong.Any(issue => issue.IsError) ? new Color(1f, 0.35f, 0.3f) : new Color(1f, 0.8f, 0.3f);
            mark.style.unityFontStyleAndWeight = FontStyle.Bold;
            mark.style.unityTextAlign = TextAnchor.MiddleCenter;
            mark.style.marginRight = 4f;
            view.titleContainer.Add(mark);
        }

        // The node holds the record with this id: its block
        private static bool OwnsNode(AgentState state, int id)
        {
            foreach (AgentTask task in state.Tasks)
            {
                if (task.Id == id)
                    return true;
            }

            return false;
        }

        /// <summary>Selects the node of a record and brings it to the middle of the canvas.</summary>
        public void FocusNode(int id)
        {
            GraphElement found = _nodes.TryGetValue(id, out StateNode state) ? state
                : (GraphElement)_sensors.Values.FirstOrDefault(sensor => sensor.Sensor.Id == id)
                  ?? graphElements.OfType<ShapeNode>().FirstOrDefault(shape => shape.Shape.Id == id)
                  ?? (GraphElement)_conditionOwners.Keys.FirstOrDefault(condition => condition.Condition.Id == id)
                  ?? (GraphElement)_valueViews.Values.FirstOrDefault(value => value.Value.Id == id)
                  ?? _nodes.Values.FirstOrDefault(node => OwnsNode(node.State, id));
            if (found == null)
            {
                // Not in this view: the view that holds it opens (its subgraph, or the graph itself), and it is found there
                if (_document != null && !_reopening && ViewHolding(id, out GraphSubgraph holder) && holder != _scope)
                {
                    _reopening = true;
                    OpenSubgraph(holder);
                    _reopening = false;
                    schedule.Execute(() => FocusNode(id)).ExecuteLater(40);
                }

                return;
            }

            ClearSelection();
            AddToSelection(found);
            if (panel != null)
                FrameSelection();                       // a canvas in no window has no view to bring it into
        }

        private bool _reopening;

        // The view a node of the graph stands in: the subgraph of its node (null — the graph itself); false — no such node
        private bool ViewHolding(int id, out GraphSubgraph holder)
        {
            holder = null;
            foreach (AgentState state in _document.States())
            {
                if (state.Id != id && !OwnsNode(state, id))
                    continue;

                holder = _document.SubgraphOf(state.Id);
                return true;
            }

            return false;
        }

        private const string COMPARE = "aikit.condition.compare";

        // ---------- the one list of nodes (step 90, C1): right click, Space, a pin let go over an empty place ----------

        /// <summary>
        /// Opens the list of nodes with search at a point of the screen; what is picked is added at a point of the canvas.
        /// asker — what a condition picked here answers to (a Branch, a decision, an AND / OR; null — a new Branch).
        /// </summary>
        private void OpenList(Vector2 screen, Vector2 at, string title, List<NodeMenu.Item> items, object asker,
            Action<object> made = null)
        {
            if (_document == null || items == null || items.Count == 0)
                return;

            SearchWindow.Open(new SearchWindowContext(screen), NodeLibrary.Of(title, items, item =>
            {
                object added = AddPicked(item, at, asker);
                made?.Invoke(added);
            }));
        }

        // The screen point of a point of the panel (the window's corner plus it)
        private static Vector2 ScreenOf(Vector2 panelPoint)
        {
            EditorWindow window = EditorWindow.focusedWindow;
            return (window != null ? window.position.position : Vector2.zero) + panelPoint;
        }

        /// <summary>
        /// An item picked in the list, added where the list was opened: a node, a shape into Detection, a sensor of its
        /// own, a value node, a condition into what asks it — the picked Branch / decision / AND / OR when nothing was given.
        /// </summary>
        public object AddPicked(NodeMenu.Item item, Vector2 at, object asker = null)
        {
            if (_document == null)
                return null;

            if (item.Act is NodeMenu.Act.Condition or NodeMenu.Act.Flag)
                asker ??= PickedAsker();
            return NodeMenu.Add(_document, item, at, asker);
        }

        /// <summary>A node of the list by its registry id, added at a point of the canvas (the Brain, a decision, a block…).</summary>
        public void AddFromList(string id, Vector2 at)
        {
            if (_document == null)
                return;

            foreach (NodeMenu.Item item in NodeMenu.Everything(_document))
            {
                if (item.Id == id)
                {
                    AddPicked(item, at);
                    return;
                }
            }
        }

        // What a condition picked from the whole list answers to: a picked AND / OR, else a picked node that asks one
        private object PickedAsker()
        {
            object joining = selection.OfType<ConditionNode>()
                .Where(node => GraphDocument.InputsOf(node.Condition) != null)
                .Select(node => (object)node.Condition).FirstOrDefault();
            return joining ?? selection.OfType<StateNode>().Select(node => node.ConditionAsker()).FirstOrDefault(found => found != null);
        }

        // What the arrow from this pin does when it reaches a node (null — not a pin an arrow leaves)
        private Action<AgentState> Connector(Port output)
        {
            if (output == null || output.direction != Direction.Output || output.portType != typeof(Transition))
                return null;

            if (output.userData is Transition existing)
                return to => _document.Retarget(existing, to);      // dragged from the row of an arrow: it now leads here

            AgentState from = (output.node as StateNode)?.State;
            if (from == null)
                return null;

            if (output.userData is RandomPin numbered)
                return to => _document.ConnectOutput(from, to, numbered.Index);

            TransitionTrigger trigger = output.userData is TransitionTrigger own ? own : TransitionTrigger.Always;
            return to => _document.Connect(from, to, trigger);
        }

        // The variables of the blackboard that fit a data pin: picked — a node of it appears there, wired in
        private void OfferVariables(DataLink link, Type pinType, Vector2 screen, Vector2 at)
        {
            bool any = link.Owner is CompareCondition && link.Field.Name == "Variable" ||
                       link.Owner is DebugLogTask && link.Field.Name == nameof(DebugLogTask.Value);
            List<VariableUse> uses = BlackboardUsage.Collect(_document);
            foreach (VariableUse own in BlackboardUsage.OfCharacter(ElementInspector.Character))
            {
                if (!uses.Exists(use => use.Name == own.Name))
                    uses.Add(own);
            }

            var items = new List<(string Path, string Id)>();
            foreach (VariableUse use in uses.OrderBy(use => use.Name))
            {
                if (!string.IsNullOrEmpty(use.Name) && (any || ValueFields.PinType(use.Type) == pinType) &&
                    !items.Exists(item => item.Id == use.Name))
                    items.Add((use.Name.Replace('/', ' '), use.Name));
            }

            SearchWindow.Open(new SearchWindowContext(screen), NodeLibrary.Of("Variables", items, variable =>
            {
                GraphVariableNode node = _document.AddVariableNodeAt(variable, at);
                _document.SetReader(node, link.Owner);
                if (link.Owner is CompareCondition compared && link.Field.Name == "Variable")
                    compared.Kind = _document.TypeOfVariable(variable);
                _document.BindField(link.Owner, link.Field, variable);
            }));
        }

        /// <summary>
        /// What can lead into a node, its In let go over an empty place: the blocks, the new one wired into it by its
        /// outcome (Success, or Done when it has only that). A decision is led into by the Brain only: wired to the
        /// graph's Brain at once, or — with none yet — the list offers the Brain.
        /// </summary>
        private void OfferBefore(AgentState target, Vector2 screen, Vector2 at)
        {
            if (target.Reasoner != null)
            {
                AgentState brain = _document.States().FirstOrDefault(state => state.IsBrain);
                if (brain != null)
                {
                    _document.Connect(brain, target);
                    return;
                }

                OpenList(screen, at, "Add Brain",
                    NodeMenu.Everything(_document).Where(item => item.Kind == NodeKind.Brain).ToList(), null,
                    added =>
                    {
                        if (added is AgentState made)
                            _document.Connect(made, target);
                    });
                return;
            }

            OpenList(screen, at, "Add Node", NodeMenu.After(_document, fromBrain: false, instantOnly: false), null, added =>
            {
                if (added is AgentState from && OutcomeInto(from) is { } trigger)
                    _document.Connect(from, target, trigger);
            });
        }

        /// <summary>The pin a new node leads on by into the next one: Done, else Success; null — it never finishes.</summary>
        public static TransitionTrigger? OutcomeInto(AgentState state)
        {
            if (state == null || !state.HasForegroundTasks)
                return null;

            (string success, string failure, string finished) = StateNode.OutcomeNames(state);
            return finished != null ? TransitionTrigger.TasksFinished
                : success != null ? TransitionTrigger.TasksSucceeded
                : failure != null ? TransitionTrigger.TasksFailed
                : null;
        }

        /// <summary>
        /// What reads a value, its pin let go over an empty place (a Get's number, a What, Try Get Component's Component or
        /// Found): the nodes of the list with a pin that takes it — the new one appears wired. A condition comes with a
        /// Branch of its own that asks it.
        /// </summary>
        private void OfferReaders(AgentValue given, Vector2 screen, Vector2 at)
        {
            var fields = new Dictionary<string, string>();
            List<NodeMenu.Item> items = ReadersOf(_document, given, fields);
            OpenList(screen, at, "Add Reader", items, null, added =>
            {
                AgentNode reader = ReaderOf(added);
                if (reader is BranchTask branch)
                {
                    // A flag into a Branch: its question is a Compare "is it on" of this flag
                    if (_document.AddConditionTo(branch, COMPARE_ID, at + new Vector2(-40f, 90f)) is CompareCondition isOn)
                        _document.WireValue(given, isOn, nameof(CompareCondition.A));
                    return;
                }

                if (reader != null && fields.TryGetValue(NodeOf(reader), out string field))
                    _document.WireValue(given, reader, field);
            });
        }

        /// <summary>
        /// The nodes of the list that read this value, each with the pin it comes into (by the node's registry id; for
        /// tests). A pin that takes any value takes a number; a flag only a Compare (in A) or a Debug Log.
        /// </summary>
        public static List<NodeMenu.Item> ReadersOf(GraphDocument document, AgentValue given, Dictionary<string, string> fields)
        {
            var items = new List<NodeMenu.Item>();
            if (given == null)
                return items;

            foreach (NodeMenu.Item item in NodeMenu.Everything(document))
            {
                if (item.Act is not (NodeMenu.Act.Node or NodeMenu.Act.Value or NodeMenu.Act.Condition) ||
                    AIKitNodeRegistry.Create(item.Id) is not AgentNode probe)
                    continue;

                foreach (GraphFields.Link link in ValueFields.ValueLinks(probe))
                {
                    if (!link.ElementType.IsInstanceOfType(given) || !Takes(probe, link, given))
                        continue;

                    fields[item.Id] = link.Name;
                    items.Add(item);
                    break;
                }
            }

            if (given.Kind == ValueKind.Flag)
                AddBranch(document, items, fields);

            return items;
        }

        // A pin that takes any value node takes it by its kind: a number for the numbers of Set / Add…, a flag only into a
        // Compare's A or a Debug Log
        private static bool Takes(AgentNode reader, GraphFields.Link link, AgentValue given)
        {
            if (link.ElementType != typeof(AgentValue))
                return true;                            // a pin of its own kind (As, What, Abilities)

            return given.Kind switch
            {
                ValueKind.Number => true,
                ValueKind.Flag => reader is CompareCondition && link.Name == nameof(CompareCondition.A) || reader is DebugLogTask,
                _ => false
            };
        }

        /// <summary>
        /// What reads a variable, its node's out let go over an empty place: the nodes with a pin of the variable's kind
        /// (an object — Chase, Move To, Try Get Component…), and a Compare (with a Branch) or a Debug Log for any kind. The
        /// new node appears wired to this node of the variable.
        /// </summary>
        private void OfferVariableReaders(GraphVariableNode data, Vector2 screen, Vector2 at)
        {
            var fields = new Dictionary<string, string>();
            List<NodeMenu.Item> items = VariableReadersOf(_document, _document.TypeOfVariable(data.Variable), fields);
            OpenList(screen, at, "Add Reader", items, null, added =>
            {
                AgentNode reader = ReaderOf(added);
                if (reader == null || !fields.TryGetValue(NodeOf(reader), out string name))
                    return;

                if (reader is BranchTask branch)
                {
                    // A flag variable into a Branch: its question is that flag, "is it on" (drawn as the flag itself)
                    if (_document.AddConditionTo(branch, COMPARE_ID, at + new Vector2(-40f, 90f)) is CompareCondition flag)
                    {
                        flag.Variable = data.Variable;
                        flag.Kind = VariableType.Bool;
                        flag.Expected = true;
                        _document.Touch();
                    }

                    return;
                }

                System.Reflection.FieldInfo field = reader.GetType().GetField(name);
                if (field == null)
                    return;

                _document.SetReader(data, reader);
                if (reader is CompareCondition compared && name == nameof(CompareCondition.Variable))
                    compared.Kind = _document.TypeOfVariable(data.Variable);
                _document.BindField(reader, field, data.Variable);
            });
        }

        /// <summary>The nodes of the list that read a variable of this kind, each with its field (by registry id; for tests).</summary>
        public static List<NodeMenu.Item> VariableReadersOf(GraphDocument document, VariableType kind,
            Dictionary<string, string> fields)
        {
            var items = new List<NodeMenu.Item>();
            Type pinType = ValueFields.PinType(kind);
            foreach (NodeMenu.Item item in NodeMenu.Everything(document))
            {
                if (item.Act is not (NodeMenu.Act.Node or NodeMenu.Act.Value or NodeMenu.Act.Condition) ||
                    AIKitNodeRegistry.Create(item.Id) is not AgentNode probe)
                    continue;

                string field = probe switch
                {
                    CompareCondition => nameof(CompareCondition.Variable),     // compares a variable of any kind
                    DebugLogTask => nameof(DebugLogTask.Value),                 // writes a variable of any kind
                    _ => ValueFields.Pins(probe).FirstOrDefault(pin => pin.FieldType == typeof(string) &&
                                                                       ValueFields.PinType(pin) == pinType)?.Name
                };
                if (field == null)
                    continue;

                fields[item.Id] = field;
                items.Add(item);
            }

            if (kind == VariableType.Bool)
                AddBranch(document, items, fields);

            return items;
        }

        // A flag leads a Branch: offered with the readers, picked — a Branch asking whether the flag is on
        private static void AddBranch(GraphDocument document, List<NodeMenu.Item> items, Dictionary<string, string> fields)
        {
            foreach (NodeMenu.Item item in NodeMenu.Everything(document))
            {
                if (item.Id != BRANCH_ID || items.Exists(other => other.Id == BRANCH_ID))
                    continue;

                items.Insert(0, item);
                fields[item.Id] = nameof(BranchTask.Condition);
            }
        }

        private const string BRANCH_ID = "aikit.task.branch";
        private const string COMPARE_ID = "aikit.condition.compare";

        /// <summary>
        /// The object variables a sensor may write into, its Object pin let go over an empty place: picked — a node of the
        /// variable appears there, the sensor writes into it.
        /// </summary>
        private void OfferSensorVariables(SensorTask sensor, Vector2 screen, Vector2 at)
        {
            var items = new List<(string Path, string Id)>();
            foreach (VariableUse use in BlackboardUsage.Collect(_document).OrderBy(use => use.Name))
            {
                if (!string.IsNullOrEmpty(use.Name) && use.Type == VariableType.Transform &&
                    !items.Exists(item => item.Id == use.Name))
                    items.Add((use.Name.Replace('/', ' '), use.Name));
            }

            SearchWindow.Open(new SearchWindowContext(screen), NodeLibrary.Of("Write Into", items, variable =>
            {
                GraphVariableNode node = _document.AddVariableNodeAt(variable, at);
                _document.SetSensorVariable(sensor, variable, node);
            }));
        }

        // The node a pick of the list made that reads: a block's task, a value node, a condition
        private static AgentNode ReaderOf(object added) => added switch
        {
            AgentState state => state.Tasks.FirstOrDefault(),
            AgentNode node => node,
            _ => null
        };

        // The registry id of a node (what the reader lists are keyed by)
        private static string NodeOf(AgentNode node) => GraphSerializer.TypeIdOf(node) ?? "";

        /// <summary>
        /// A pin was dragged and let go over an empty place: the one list opens there, holding only what the pin can be
        /// wired to, and the picked node appears already wired. A flow pin offers blocks (the Brain's Decisions pin —
        /// decisions; an On Tick pin — instant blocks); a Condition pin — conditions and the flags of the blackboard;
        /// Detection's Sensors pin — the shapes of seeing; a value pin — the value nodes that fit it; a data pin — the
        /// variables of the blackboard.
        /// </summary>
        private void OfferFor(Port pin, Vector2 mouse)
        {
            if (_document == null || pin == null)
                return;

            Vector2 screen = ScreenOf(mouse);
            Vector2 at = contentViewContainer.WorldToLocal(mouse);

            if (pin.userData is DataLink link && pin.direction == Direction.Input)
            {
                OfferVariables(link, pin.portType, screen, at + new Vector2(-170f, -10f));
                return;
            }

            if (pin.userData is SensorSlot sensors)
            {
                DetectionTask into = sensors.Detection;
                Vector2 place = at + new Vector2(-220f, -20f);
                SearchWindow.Open(new SearchWindowContext(screen), NodeLibrary.Of("Add Sensor", NodeMenu.Shapes(),
                    item => _document.AddShape(item.Id, place, into)));
                return;
            }

            if (pin.userData is ConditionSlot slot)
            {
                OpenList(screen, at + new Vector2(-200f, -20f), "Add Condition", NodeMenu.Conditions(_document), slot.Owner);
                return;
            }

            if (pin.userData is ValueSlot taking)
            {
                Vector2 place = at + new Vector2(-200f, -20f);
                SearchWindow.Open(new SearchWindowContext(screen), NodeLibrary.Of("Add Value", NodeMenu.Values(taking.Type),
                    item => _document.AddValueTo(taking.Owner, taking.Field, item.Id, place)));
                return;
            }

            // The flow's In of a node: what can lead into it (a decision: the Brain)
            if (pin.direction == Direction.Input && pin.portType == typeof(Transition) && pin.node is StateNode ledInto)
            {
                OfferBefore(ledInto.State, screen, at + new Vector2(-280f, -30f));
                return;
            }

            // A value given out — a value node's, a block's Component / Found: what reads it
            if (pin.direction == Direction.Output && GivenAt(pin) is { } given)
            {
                OfferReaders(given, screen, at + new Vector2(60f, -30f));
                return;
            }

            // A variable node's out: what reads a variable of its kind
            if (pin.node is VariableNodeView variable && pin == variable.Out)
            {
                OfferVariableReaders(variable.Data, screen, at + new Vector2(60f, -30f));
                return;
            }

            // A sensor's Object: the object variables it may write into
            if (pin.node is SensorNode sensorNode && pin == sensorNode.ObjectPort)
            {
                OfferSensorVariables(sensorNode.Sensor, screen, at + new Vector2(40f, -10f));
                return;
            }

            Action<AgentState> wire = Connector(pin);
            if (wire == null)
                return;

            AgentState source = (pin.node as StateNode)?.State;
            bool fromBrain = source != null && source.IsBrain;
            bool instantOnly = pin.userData is TransitionTrigger.Tick || source != null && IsInTickChain(source);
            OpenList(screen, at + new Vector2(0f, -30f), "Add Node", NodeMenu.After(_document, fromBrain, instantOnly), null,
                added =>
                {
                    if (added is AgentState state)
                        wire(state);
                });
        }

        // A Condition pin: who the condition wired into it answers to (a Branch, a decision, an AND / OR) and through
        // which of its fields (null — its first)
        private sealed class ConditionSlot
        {
            public object Owner;
            public string Field;
        }

        // A value pin: the node that reads the value, its link field, the type of value it takes, one or many
        private sealed class ValueSlot
        {
            public AgentNode Owner;
            public string Field;
            public Type Type;
            public bool IsList;
        }

        // The wire from a value node into a reader's pin: deleting it pulls the wire out (the value node stays)
        private sealed class ValueWire
        {
            public AgentValue Value;
            public AgentNode Reader;
            public string Field;
        }

        /// <summary>A node of the canvas: its pins tell the canvas when a wire is let go over an empty place.</summary>
        private class CanvasNode : Node
        {
            public override Port InstantiatePort(Orientation orientation, Direction direction, Port.Capacity capacity, Type type) =>
                DropPort.Make(orientation, direction, capacity, type);
        }

        private sealed class DropPort : Port
        {
            private DropPort(Orientation orientation, Direction direction, Capacity capacity, Type type)
                : base(orientation, direction, capacity, type)
            {
            }

            public static DropPort Make(Orientation orientation, Direction direction, Capacity capacity, Type type)
            {
                var port = new DropPort(orientation, direction, capacity, type);
                port.m_EdgeConnector = new EdgeConnector<Edge>(new DropListener());
                port.AddManipulator(port.m_EdgeConnector);
                return port;
            }
        }

        private sealed class DropListener : IEdgeConnectorListener
        {
            public void OnDropOutsidePort(Edge edge, Vector2 position)
            {
                Port pin = edge.output ?? edge.input;
                GraphViewCanvas canvas = pin?.GetFirstAncestorOfType<GraphViewCanvas>();
                if (canvas == null)
                    return;

                // Let go over a pin that does not take it: say why, instead of opening the list
                VisualElement under = pin.panel?.Pick(position);
                Port target = under as Port ?? under?.GetFirstAncestorOfType<Port>();
                if (target != null && target != pin)
                {
                    canvas.Refuse(pin, target);
                    return;
                }

                // A decision's In let go over the Brain or anywhere on its frame: wired to the Brain from there (C10)
                StateNode brain = BrainUnder(under);
                if (brain != null && pin.direction == Direction.Input && pin.portType == typeof(Transition))
                {
                    canvas.WireToBrain(pin, brain, position);
                    return;
                }

                // An arrow let go over the body of a node: into that node's In, as if let go on the pin
                StateNode node = under as StateNode ?? under?.GetFirstAncestorOfType<StateNode>();
                if (node != null && node != pin.node && pin.direction == Direction.Output &&
                    pin.portType == typeof(Transition) && node.In != null && canvas.MayConnect(pin, node.In))
                {
                    Action<AgentState> wire = canvas.Connector(pin);
                    if (wire != null)
                    {
                        AgentState into = node.State;
                        canvas.schedule.Execute(() => wire(into));
                        return;
                    }
                }

                canvas.OfferFor(pin, position);
            }

            // The Brain under a place: its node, or the frame drawn round it
            private static StateNode BrainUnder(VisualElement under)
            {
                for (VisualElement at = under; at != null; at = at.parent)
                {
                    if (at is StateNode { State: { IsBrain: true } } brain)
                        return brain;
                    if (at is FrameGroup { Brain: { } framed })
                        return framed;
                }

                return null;
            }

            // What GraphView does by itself for a wire dropped on a pin
            public void OnDrop(GraphView graphView, Edge edge)
            {
                var replaced = new List<GraphElement>();
                if (edge.input.capacity == Port.Capacity.Single)
                    replaced.AddRange(edge.input.connections.Where(other => other != edge));
                if (edge.output.capacity == Port.Capacity.Single)
                    replaced.AddRange(edge.output.connections.Where(other => other != edge));
                if (replaced.Count > 0)
                    graphView.DeleteElements(replaced);

                var created = new List<Edge> { edge };
                if (graphView.graphViewChanged != null)
                    created = graphView.graphViewChanged(new GraphViewChange { edgesToCreate = created }).edgesToCreate;

                foreach (Edge made in created)
                {
                    graphView.AddElement(made);
                    edge.input.Connect(made);
                    edge.output.Connect(made);
                }
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter) =>
            startPort.userData is ConditionSlot || startPort.userData is SensorSlot ? new List<Port>() :    // a condition is picked from the list, not wired by hand
            ports.Where(port => port.node != startPort.node && port.direction != startPort.direction &&
                                (port.portType == startPort.portType || TakesAnyVariable(port, startPort) ||
                                 TakesAnyVariable(startPort, port)) && port.node is not WirePoint &&
                                port.node is not LinkNode && MayConnect(startPort, port)).ToList();

        // The variable pin of a Compare takes a variable of any kind: the node compares it the way its kind allows
        // The node is reached from an On Tick pin (directly or along a chain of instant nodes)
        private bool IsInTickChain(AgentState state)
        {
            var seen = new HashSet<AgentState>();
            var open = new List<AgentState> { state };
            while (open.Count > 0)
            {
                AgentState at = open[^1];
                open.RemoveAt(open.Count - 1);
                if (!seen.Add(at))
                    continue;

                foreach (AgentState other in _document.States())
                {
                    foreach (Transition arrow in other.Transitions)
                    {
                        if (arrow.Target != at)
                            continue;
                        if (arrow.Trigger == TransitionTrigger.Tick)
                            return true;
                        if (other.IsInstant)
                            open.Add(other);
                    }
                }
            }

            return false;
        }

        private static bool TakesAnyVariable(Port pin, Port other) =>
            (pin.userData is DataLink { Owner: CompareCondition, Field: { Name: "Variable" } } ||
             pin.userData is DataLink { Owner: DebugLogTask, Field: { Name: nameof(DebugLogTask.Value) } }) && other.node is VariableNodeView;

        /// <summary>Why a wire from one pin cannot come into the other — said to the user when it is dropped there.</summary>
        private string WhyNot(Port from, Port to)
        {
            if (to.node == from.node)
                return "A node can't be wired to itself";
            if (to.direction == from.direction)
                return to.direction == Direction.Input ? "Both are inputs: wire an output into an input" : "Both are outputs: wire an output into an input";
            if (from.userData is ConditionSlot || to.userData is ConditionSlot)
                return "A condition is picked from the list: let go of the wire in an empty place";

            Port output = from.direction == Direction.Output ? from : to;
            Port input = from.direction == Direction.Output ? to : from;
            if (output.portType != input.portType)
                return $"{Kind(output.portType)} can't go into a pin that takes {Kind(input.portType)}";
            if (input.userData is ValueSlot slot && GivenAt(output) is { } givenValue && !slot.Type.IsInstanceOfType(givenValue))
                return $"'{NodeTitles.Shown(givenValue)}' can't go into {ObjectNames.NicifyVariableName(slot.Field)}: it takes " +
                       $"{NodeTitles.Of(slot.Type) ?? ObjectNames.NicifyVariableName(slot.Type.Name)}";
            if (output.node is StateNode { State: { IsBrain: true } })
                return "The Brain's Decisions pin takes decisions only (Fight, Chase, Patrol…)";
            if (input.node is StateNode { State: { Reasoner: not null } })
                return "A decision is wired only from the Brain's Decisions pin";
            if (output.node is StateNode && input.node is StateNode)
                return "A node is wired only to nodes of its own brain: brains share data through the blackboard";

            return "These pins don't fit each other";
        }

        private static string Kind(Type pinType) =>
            pinType == typeof(bool) ? "a flag"
            : pinType == typeof(float) ? "a number"
            : pinType == typeof(Transform) ? "an object"
            : pinType == typeof(Transition) ? "the flow"
            : typeof(AgentValue).IsAssignableFrom(pinType) ? "a value node"
            : pinType.Name;

        private void Refuse(Port from, Port to)
        {
            EditorWindow window = EditorWindow.focusedWindow;
            if (window != null)
                window.ShowNotification(new GUIContent(WhyNot(from, to)), 2.5);
        }

        // Arrows stay inside a brain: a node is wired to the nodes of its own brain only. The deciding brain is the
        // one that reaches across — its rules lead to brains (their Enter), and to nothing else
        private bool MayConnect(Port a, Port b)
        {
            Port from = a.direction == Direction.Output ? a : b;
            Port to = a.direction == Direction.Output ? b : a;

            // The edge of a subgraph shows the wires that cross it; a new one is drawn inside it or with it expanded
            if (from.userData is BoundaryPin || to.userData is BoundaryPin)
                return false;

            // A value node into a value pin that takes its kind (a Get into a number pin, an Ability into Abilities)
            if (to.userData is ValueSlot || GivenAt(from) != null)
                return to.userData is ValueSlot slot && GivenAt(from) is { } given && given != slot.Owner &&
                       (given as IBlockOutput)?.Block != slot.Owner && slot.Type.IsInstanceOfType(given);

            if (from.node is not StateNode source || to.node is not StateNode target || _document == null)
                return true;

            if (source.State.IsBrain)
                return from.userData == StateNode.DecisionsPin && target.State.Reasoner != null;

            // A decision is the Brain's: nothing else leads into it (its chain starts at its own Do pin)
            if (target.State.Reasoner != null)
                return false;

            // On Tick and the chain after it: instant nodes only (they run and the flow comes back)
            bool inChain = from.userData is TransitionTrigger.Tick || source.State.IsInstant && !source.State.IsBrain;
            if (from.userData is TransitionTrigger.Tick && !target.State.IsInstant)
                return false;
            if (inChain && from.userData is not TransitionTrigger.Tick && IsInTickChain(source.State) && !target.State.IsInstant)
                return false;

            return !target.State.IsBrain && _document.BrainOf(source.State.Id) == _document.BrainOf(target.State.Id);
        }

        public override void AddToSelection(ISelectable selectable)
        {
            base.AddToSelection(selectable);

            // A link label picked: the node it leads to shows itself
            StateNode pointed = null;
            if (selectable is LinkNode picked && picked.Transition.Target != null)
                _nodes.TryGetValue(picked.Transition.Target.Id, out pointed);
            Point(pointed);
            RefreshEmphasis();
            Selected?.Invoke(SelectedElement);
        }

        public override void RemoveFromSelection(ISelectable selectable)
        {
            base.RemoveFromSelection(selectable);
            RefreshEmphasis();
        }

        public override void ClearSelection()
        {
            base.ClearSelection();
            Point(null);
            RefreshEmphasis();
            Selected?.Invoke(null);
        }

        // A node is picked: its wires (all the way, through their bend points and link labels) stay bright and get
        // thicker, the rest fade. Nothing picked, or only wires — every wire is plain
        private void RefreshEmphasis()
        {
            if (_rebuilding)
                return;

            var own = new HashSet<object>();
            bool anyNode = false;
            foreach (ISelectable item in selection)
            {
                if (item is not Node node || node is WirePoint)
                    continue;

                anyNode = true;
                foreach (Port port in node.Query<Port>().ToList())
                {
                    foreach (Edge edge in port.connections)
                        own.Add(edge.userData ?? edge);
                }

                // An arrow drawn as a link does not reach the node it leads to: it is this node's wire all the same
                if (node is StateNode state)
                {
                    foreach (LiveEdge live in _edges)
                    {
                        if (live.Transition.Target == state.State)
                            own.Add(live.Transition);
                    }
                }
            }

            foreach (Edge edge in edges.ToList())
            {
                if (edge is WireEdge wire)
                    wire.SetEmphasis(!anyNode ? 0 : own.Contains(edge.userData ?? edge) ? 1 : -1);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (_document == null)
                return;

            Vector2 screen = ScreenOf(evt.mousePosition);
            if (evt.target is ConditionNode conditionNode)
            {
                // AND / OR: one more input, picked from the same list a Condition pin opens
                if (GraphDocument.InputsOf(conditionNode.Condition) != null)
                {
                    AgentCondition joining = conditionNode.Condition;
                    Vector2 near = conditionNode.GetPosition().position + new Vector2(-260f, -40f);
                    evt.menu.AppendAction("Add Input", _ => schedule.Execute(() =>
                        OpenList(screen, near, "Add Input", NodeMenu.Conditions(_document), joining)));
                }

                evt.menu.AppendAction("Delete", _ => DeleteSelection());
                return;
            }

            if (evt.target is StateNode node)
            {
                // A node that asks a question (a Branch, a decision): one more condition, from the same list
                if (node.ConditionAsker() is { } asker)
                {
                    Vector2 near = node.GetPosition().position + new Vector2(-260f, -120f);
                    evt.menu.AppendAction("Add Condition", _ => schedule.Execute(() =>
                        OpenList(screen, near, "Add Condition", NodeMenu.Conditions(_document), asker)));
                }

                AppendNaming(evt, node, node.State);
                evt.menu.AppendAction("Frame Selection", _ => AddFrame(node));   // a frame round the picked nodes
                if (_scope == null && !node.State.IsBrain)
                    evt.menu.AppendAction("Collapse To Subgraph", _ => CollapseSelection(node));
                bool stops = GraphBreakpoints.Has(_document.Asset, node.State.Id);
                evt.menu.AppendAction(stops ? "Remove Breakpoint" : "Add Breakpoint", _ =>
                {
                    GraphBreakpoints.Set(_document.Asset, node.State.Id, !stops);
                    Show(_document);
                });
                evt.menu.AppendAction("Delete", _ => DeleteSelection());
                return;
            }

            if (evt.target is SubgraphView partView)
            {
                GraphSubgraph part = partView.Part;
                evt.menu.AppendAction("Open", _ => schedule.Execute(() => OpenSubgraph(part)));
                evt.menu.AppendAction("Rename", _ => schedule.Execute(() => RenameSubgraph(partView)));
                evt.menu.AppendAction("Expand Here (the nodes come back)",
                    _ => schedule.Execute(() => _document.ExpandSubgraph(part)));
                return;
            }

            if (evt.target is SensorNode sensorNode)
            {
                AppendNaming(evt, sensorNode, sensorNode.Sensor);
                evt.menu.AppendAction("Frame Selection", _ => AddFrame(sensorNode));
                evt.menu.AppendAction("Delete", _ => DeleteSelection());
                return;
            }

            if (evt.target is ShapeNode shapeNode)
            {
                AppendNaming(evt, shapeNode, shapeNode.Shape);
                evt.menu.AppendAction("Delete", _ => DeleteSelection());
                return;
            }

            if (evt.target is ValueNodeView valueView)
            {
                AppendNaming(evt, valueView, valueView.Value);
                evt.menu.AppendAction("Delete", _ => DeleteSelection());
                return;
            }

            if (evt.target is FrameGroup clickedFrame)
            {
                // The Brain's frame is the Brain's: taking the frame away would not take the Brain (step 90) — the Brain
                // itself is deleted by name
                if (clickedFrame.Brain != null)
                {
                    AgentState brain = clickedFrame.Brain.State;
                    evt.menu.AppendAction("Delete Brain", _ => schedule.Execute(() => _document.Remove(brain)));
                }
                else
                {
                    evt.menu.AppendAction("Remove Frame (the nodes stay)",
                        _ => schedule.Execute(() => _document.RemoveFrame(clickedFrame.Data)));
                }

                return;
            }

            if (evt.target is VariableNodeView variableView)
            {
                Vector2 duplicateAt = contentViewContainer.WorldToLocal(evt.mousePosition);
                evt.menu.AppendAction("Duplicate", _ => _document.DuplicateVariableNode(variableView.Data, duplicateAt));
                evt.menu.AppendAction("Delete", _ => _document.RemoveVariableNode(variableView.Data));
                return;
            }

            if (evt.target is LinkNode linkLabel)
            {
                Transition linked = linkLabel.Transition;
                evt.menu.AppendAction("Go To Node", _ => GoTo(linked.Target));
                if (!_document.IsDocked(linked))
                    evt.menu.AppendAction("Dock To Pin", _ => _document.DockLink(linked));
                evt.menu.AppendAction("Show As Wire", _ => _document.ClearLink(linked));
                evt.menu.AppendAction("Delete Arrow", _ => schedule.Execute(() => _document.Remove(linked)));
                return;
            }

            // The wire under the mouse: the event may come from the wire itself or from the line drawn inside it
            Edge clicked = evt.target as Edge ?? (evt.target as VisualElement)?.GetFirstAncestorOfType<Edge>();
            if (clicked != null)
            {
                // An arrow between nodes: drawn short — a label "→ node" at its pin — or all the way
                if (clicked is { userData: Transition arrow } && _segments.ContainsKey(clicked) &&
                    !(_sources.TryGetValue(arrow, out Node source) && source is StateNode { State: { IsBrain: true } }))
                {
                    if (_document.IsLink(arrow))
                        evt.menu.AppendAction("Show As Wire", _ => _document.ClearLink(arrow));
                    else
                        evt.menu.AppendAction("Replace With Link", _ => _document.SetLink(arrow));
                }

                evt.menu.AppendAction("Delete", _ => DeleteSelection());
                return;
            }

            if (evt.target is not GraphView)
                return;

            // An empty place: the one list of nodes with search, where the click was (no menu of its own — C1)
            Vector2 at = contentViewContainer.WorldToLocal(evt.mousePosition);
            List<NodeMenu.Item> everything = NodeMenu.Everything(_document);
            schedule.Execute(() => OpenList(screen, at, "Add Node", everything, null));
        }

        // "Rename" (the title becomes a field to type the label in) and, for a labelled node, "Name By Type"
        private void AppendNaming(ContextualMenuPopulateEvent evt, Node view, AgentNode node)
        {
            if (node == null || _document.IsReadOnly)
                return;

            evt.menu.AppendAction("Rename", _ => schedule.Execute(() => BeginRename(view, node)));
            if (NodeTitles.IsLabelled(node))
                evt.menu.AppendAction("Name By Type", _ => schedule.Execute(() => _document.NameByType(node)));
        }

        private GraphViewChange OnChanged(GraphViewChange change)
        {
            if (_rebuilding || _document == null)
                return change;

            if (change.movedElements != null)
            {
                // A frame dragged: everything inside it moved with it
                var moved = new HashSet<GraphElement>(change.movedElements);
                foreach (Group draggedFrame in change.movedElements.OfType<Group>())
                    moved.UnionWith(draggedFrame.containedElements);

                foreach (GraphElement element in moved)
                {
                    if (element is StateNode node)
                        _document.Move(node.State, node.GetPosition().position);
                    else if (element is SubgraphView movedPart)
                        _document.MoveSubgraph(movedPart.Part, movedPart.GetPosition().position);
                    else if (element is SensorNode sensor)
                        _document.MoveNode(sensor.Sensor, sensor.GetPosition().position);
                    else if (element is ShapeNode shapeView)
                        _document.MoveNode(shapeView.Shape, shapeView.GetPosition().position);
                    else if (element is ConditionNode movedCondition)
                        _document.MoveNode(movedCondition.Condition, movedCondition.GetPosition().position);
                    else if (element is ValueNodeView movedValue)
                        _document.MoveNode(movedValue.Value, movedValue.GetPosition().position);
                    else if (element is WirePoint point)
                        _document.MoveWaypoint(point.Wire, point.Index, point.GetPosition().position);
                    else if (element is LinkNode movedLabel && !moved.OfType<Node>().Any(other => other is not LinkNode &&
                                 _sources.TryGetValue(movedLabel.Transition, out Node from) && from == other))
                    {
                        // Dragged on its own: the label leaves its pin and stands where it was put ("Dock To Pin" — back)
                        foreach (List<LinkNode> stack in _docked.Values)
                            stack.Remove(movedLabel);
                        movedLabel.SetDocked(false);
                        _document.MoveLink(movedLabel.Transition, movedLabel.GetPosition().position);
                    }
                }

                // A variable node moved: its place is kept (its wires stay its own)
                var movedVariables = moved.OfType<VariableNodeView>().ToList();
                if (movedVariables.Count > 0)
                    schedule.Execute(() =>
                    {
                        foreach (VariableNodeView view in movedVariables)
                            _document.MoveVariableNode(view.Data, view.GetPosition().position);
                    });
            }

            if (change.edgesToCreate != null)
            {
                var created = change.edgesToCreate.ToList();
                change.edgesToCreate.Clear();
                schedule.Execute(() =>
                {
                    foreach (Edge edge in created)
                        Create(edge);
                });
            }

            if (change.elementsToRemove != null)
            {
                // A subgraph's node deleted: the subgraph opens up on the graph — its nodes are never deleted with it
                var expanded = change.elementsToRemove.OfType<SubgraphView>().Select(view => view.Part).ToList();
                change.elementsToRemove.RemoveAll(element => element is SubgraphView || element is BoundaryView);
                if (expanded.Count > 0)
                    schedule.Execute(() => expanded.ForEach(part => _document.ExpandSubgraph(part)));
            }

            if (change.elementsToRemove != null)
            {
                // Everything picked is deleted together, whatever its kind. A frame goes alone — GraphView hands over
                // what is inside it as well, and the nodes of a frame are not deleted with it. Copies of a variable
                // go with the wires they fed; points of a wire go one by one, the later first (the earlier indices
                // stay true)
                // A row of the Blackboard picked and deleted: the variable itself goes (GraphDocument.DeleteVariable)
                var deletedVariables = change.elementsToRemove.OfType<BlackboardField>()
                    .Select(BlackboardPanel.VariableOf).Where(name => name != null).ToList();
                change.elementsToRemove.RemoveAll(element => element is BlackboardField);
                if (deletedVariables.Count > 0)
                    schedule.Execute(() => deletedVariables.ForEach(name => _document.DeleteVariable(name)));

                var frames = change.elementsToRemove.OfType<FrameGroup>().ToList();
                var inFrames = new HashSet<GraphElement>(frames.SelectMany(frame => frame.containedElements));
                var views = change.elementsToRemove.OfType<VariableNodeView>().Where(view => !inFrames.Contains(view)).ToList();
                var points = change.elementsToRemove.OfType<WirePoint>().OrderByDescending(point => point.Index).ToList();
                var pointSet = new HashSet<GraphElement>(points);
                change.elementsToRemove.RemoveAll(element =>
                    element is FrameGroup || element is VariableNodeView || element is WirePoint || inFrames.Contains(element) ||
                    // the stretches of a wire GraphView hands over with its point are not "delete the arrow"
                    element is Edge stretch && (pointSet.Contains(stretch.input?.node) || pointSet.Contains(stretch.output?.node)));
                if (frames.Count > 0 || views.Count > 0 || points.Count > 0)
                    schedule.Execute(() =>
                    {
                        foreach (FrameGroup frame in frames)
                            _document.RemoveFrame(frame.Data);
                        foreach (WirePoint point in points)
                            _document.RemoveWaypoint(point.Wire, point.Index);
                        foreach (VariableNodeView view in views)
                            _document.RemoveVariableNode(view.Data);
                        Show(_document);
                    });
            }

            if (change.elementsToRemove != null)
            {
                var removed = change.elementsToRemove
                    .Select(element => element switch
                    {
                        StateNode node => (object)node.State,
                        SensorNode sensor => sensor.Sensor,
                        ShapeNode shapeView => new ShapeWire { Detection = shapeView.Detection, Shape = shapeView.Shape },
                        Edge { userData: ShapeWire shapeWire } => shapeWire,
                        Edge { userData: DataLink wire } => wire,
                        Edge { userData: ConditionWire asked } => asked,
                        ConditionNode deletedCondition => new ConditionWire { Condition = deletedCondition.Condition },
                        Edge { userData: ValueWire valueWire } => valueWire,
                        ValueNodeView deletedValue => deletedValue.Value,
                        Edge { userData: Transition transition } => transition,
                        LinkNode deletedLabel => deletedLabel.Transition,      // the label is the end of its arrow
                        _ => null
                    })
                    .Where(element => element != null).Distinct().ToList();
                bool hadMarkers = change.elementsToRemove.Count > removed.Count;
                change.elementsToRemove.Clear();
                if (removed.Count > 0)
                    schedule.Execute(() =>
                    {
                        foreach (object element in removed)
                        {
                            if (element is SensorTask sensor)
                                _document.RemoveSensor(sensor);
                            else if (element is ShapeWire shapeWire)
                                _document.RemoveShape(shapeWire.Detection, shapeWire.Shape);
                            else if (element is DataLink wire)
                                _document.BindField(wire.Owner, wire.Field, "");    // unplugged: its own value again
                            else if (element is ConditionWire asked)
                                _document.RemoveCondition(asked.Condition);
                            else if (element is ValueWire pulled)
                                _document.UnwireValue(pulled.Value, pulled.Reader, pulled.Field);   // the node stays
                            else
                                _document.Remove(element);
                        }
                    });
                else if (hadMarkers)
                    schedule.Execute(() => Show(_document));     // a marker edge (Start, parent) is not a thing to delete
            }

            return change;
        }

        private void Create(Edge edge)
        {
            if (edge.input.userData is DataLink wire)
            {
                // A wire into a node's object pin: from a variable node, or straight from the sensor that fills it
                string variable = edge.output.node switch
                {
                    VariableNodeView view => view.Data.Variable,
                    SensorNode source => source.Sensor.StoreIn,
                    _ => null
                };
                if (variable != null)
                {
                    // The wire stays on the node it was drawn from (from the sensor itself — on the node the sensor fills)
                    GraphVariableNode feeder = edge.output.node is VariableNodeView drawnFrom
                        ? drawnFrom.Data
                        : _document.SourceNode(variable);
                    _document.SetReader(feeder, wire.Owner);
                    if (wire.Owner is CompareCondition compared && wire.Field.Name == "Variable")
                        compared.Kind = _document.TypeOfVariable(variable);      // compared the way its kind allows
                    _document.BindField(wire.Owner, wire.Field, variable);
                }

                return;
            }

            if (edge.input.node is VariableNodeView filled)
            {
                // The sensor's Object pin wired into a node of a variable: the sensor writes into that variable now
                // (its own variable already — the arrow just comes into this node of it)
                if (edge.output.node is not SensorNode filler)
                    Show(_document);
                else if (filler.Sensor.StoreIn == filled.Data.Variable)
                    _document.SetSource(filled.Data);
                else if (_document.TypeOfVariable(filled.Data.Variable) == VariableType.Transform)
                    _document.SetSensorVariable(filler.Sensor, filled.Data.Variable, filled.Data);
                else
                    Show(_document);                    // a sensor writes an object: not into a number or a flag
                return;
            }

            // A value node wired into a value pin: the reader takes it (one value node may feed several pins)
            if (edge.input.userData is ValueSlot slot && GivenAt(edge.output) is { } wired)
            {
                if (!_document.WireValue(wired, slot.Owner, slot.Field))
                    Show(_document);
                return;
            }

            AgentState from = (edge.output.node as StateNode)?.State;
            AgentState to = (edge.input.node as StateNode)?.State;
            if (to == null)
                return;

            // A wire pulled from the rim of the Brain's frame leaves the frame there
            if (from != null && from.IsBrain && edge.output.userData == StateNode.DecisionsPin &&
                _pendingExit is { } exit)
            {
                _pendingExit = null;
                Transition made = _document.Connect(from, to);
                _document.SetExit(made, exit.Side, exit.Along);
                return;
            }

            if (from != null || edge.output.node is not StateNode)
                Connector(edge.output)?.Invoke(to);
        }

        /// <summary>The sensors light up while they see something (Play Mode; null — nothing to show).</summary>
        public void HighlightSensors(Blackboard board)
        {
            foreach (KeyValuePair<SensorTask, SensorNode> pair in _sensors)
            {
                Transform seen = board != null ? board.Get<Transform>(pair.Key.StoreIn) : null;
                pair.Value.SetSeen(seen);
            }
        }

        /// <summary>
        /// A sensor on the canvas: works beside whatever runs and writes what it sees into a variable (its Object pin) —
        /// that is all it does: the Brain decides what to do about it.
        /// </summary>
        private static readonly Color ShapeColor = new(0.35f, 0.8f, 0.7f);

        // The Sensors pin of Detection
        private sealed class SensorSlot
        {
            public DetectionTask Detection;
        }

        // The wire of a sensor into its Detection: deleting it takes the sensor out
        private sealed class ShapeWire
        {
            public DetectionTask Detection;
            public SensorTask Shape;
        }

        /// <summary>
        /// A sensor wired into Detection: its name, its own settings, the pins of the objects it needs (Zone: the object
        /// that is the zone — by a wire, never hidden in the node) and the pin it goes into Detection by.
        /// </summary>
        private sealed class ShapeNode : CanvasNode
        {
            public static readonly Vector2 Size = new(200f, 90f);

            private readonly GraphDocument _document;

            public ShapeNode(GraphDocument document, DetectionTask detection, SensorTask shape)
            {
                _document = document;
                Detection = detection;
                Shape = shape;
                ShowTitle(this, shape, document);
                titleContainer.style.backgroundColor = new Color(0.1f, 0.4f, 0.36f);
                titleContainer.style.height = 22f;
                titleButtonContainer.style.display = DisplayStyle.None;
                capabilities &= ~(Capabilities.Renamable | Capabilities.Collapsible | Capabilities.Copiable);

                Out = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(SensorTask));
                Out.portName = "";
                Out.portColor = ShapeColor;
                if (Out.edgeConnector != null)
                    Out.RemoveManipulator(Out.edgeConnector);      // it is wired into its Detection, and stays there
                titleContainer.Add(Out);

                // What the shape reads (its range…): each by a wire into its pin, from a node of its variable. Not the
                // interval and the variable every sensor has: Detection looks by its own and writes its own
                foreach (System.Reflection.FieldInfo field in ValueFields.ShapePins(shape))
                {
                    Port port = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single,
                        ValueFields.PinType(field));
                    port.portName = ObjectNames.NicifyVariableName(field.Name);
                    port.portColor = TypeColor(port.portType);
                    port.userData = new DataLink { Owner = shape, Field = field };
                    inputContainer.Add(port);
                    DataPorts.Add(port);
                }

                var body = new IMGUIContainer(Draw);
                body.style.paddingLeft = body.style.paddingRight = 6;
                body.style.paddingTop = body.style.paddingBottom = 3;
                body.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                extensionContainer.Add(body);
                AddRequirement(this, shape);
                style.minWidth = 190f;
                RefreshPorts();
                RefreshExpandedState();
            }

            public DetectionTask Detection { get; }

            public SensorTask Shape { get; }

            public Port Out { get; }

            /// <summary>The pins of the objects the shape works with (each fed by a wire from a node of its variable).</summary>
            public List<Port> DataPorts { get; } = new();

            private void Draw()
            {
                float labelWidth = EditorGUIUtility.labelWidth;
                ElementInspector.Compact = true;
                ElementInspector.Wired = true;
                EditorGUIUtility.labelWidth = ElementInspector.LabelWidthFor(Shape, ownOnly: true);
                EditorGUI.BeginChangeCheck();
                ElementInspector.Fields(_document, Shape, ownOnly: true);
                ElementInspector.Wired = false;
                ElementInspector.Compact = false;
                if (EditorGUI.EndChangeCheck())
                    _document.TouchQuietly();
                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        private sealed class SensorNode : CanvasNode
        {
            private readonly GraphDocument _document;
            private readonly Label _status;
            private Transform _shown;
            private bool _isLit;

            public SensorNode(GraphDocument document, SensorTask sensor)
            {
                _document = document;
                Sensor = sensor;
                ShowTitle(this, sensor, document);
                if (sensor is DetectionTask detection)
                {
                    // The sensors come in here, any number of them: let go on an empty place — the list of sensors
                    SensorsPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(SensorTask));
                    SensorsPort.portName = "Sensors";
                    SensorsPort.portColor = ShapeColor;
                    SensorsPort.userData = new SensorSlot { Detection = detection };
                    inputContainer.Add(SensorsPort);
                }
                titleContainer.style.backgroundColor = NodeKinds.TitleColor(sensor.GetType()) ?? new Color(0.45f, 0.35f, 0.12f);
                titleContainer.Insert(0, NodeIcons.ForSensor());
                capabilities &= ~Capabilities.Copiable;

                _status = new Label();
                _status.style.unityTextAlign = TextAnchor.MiddleRight;
                _status.style.marginRight = 6;
                _status.style.color = FlashColor;
                titleContainer.Add(_status);

                // What it sees, as data: wired into the "Target" pin of a node
                ObjectPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi,
                    typeof(Transform));
                ObjectPort.portName = "Object";
                ObjectPort.portColor = FillColor;
                outputContainer.Add(ObjectPort);

                var body = new IMGUIContainer(Draw);
                body.style.paddingLeft = body.style.paddingRight = 6;
                body.style.paddingTop = body.style.paddingBottom = 4;
                body.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                extensionContainer.Add(body);
                AddRequirement(this, sensor);
                style.minWidth = 240f;
                RefreshPorts();
                RefreshExpandedState();
            }

            public SensorTask Sensor { get; }

            /// <summary>The pin the sensors of Detection come into (null — not Detection).</summary>
            public Port SensorsPort { get; }

            public Port ObjectPort { get; }

            public void SetSeen(Transform seen)
            {
                bool lit = seen != null;
                if (lit == _isLit && seen == _shown)
                    return;

                _isLit = lit;
                _shown = seen;
                _status.text = lit ? $"sees {seen.name}" : "";
                Color color = lit ? FlashColor : Color.clear;
                float width = lit ? 3f : 0f;
                style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = color;
                style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = width;
                style.borderTopLeftRadius = style.borderTopRightRadius =
                    style.borderBottomLeftRadius = style.borderBottomRightRadius = 8f;
            }

            // A sensor that looks for objects by their tag says both tags it looks for (Also Tag works: so it is shown —
            // step 90), how often it looks (Detection), then its own settings
            private bool LooksByTag => !(Attribute.GetCustomAttribute(Sensor.GetType(), typeof(AIKitNodeAttribute))
                is AIKitNodeAttribute { WorksAlone: true });

            private void Draw()
            {
                float labelWidth = EditorGUIUtility.labelWidth;
                ElementInspector.Compact = true;
                ElementInspector.Wired = true;              // an object comes into a sensor only by a wire
                EditorGUIUtility.labelWidth = Mathf.Max(90f, ElementInspector.LabelWidthFor(Sensor, ownOnly: true));
                EditorGUI.BeginChangeCheck();
                ElementInspector.Field(_document, Sensor, nameof(SensorTask.Tag), "Object Tag");
                if (LooksByTag)
                    ElementInspector.Field(_document, Sensor, nameof(SensorTask.AlsoTag), "Also Tag");
                if (Sensor is DetectionTask)
                {
                    ElementInspector.Field(_document, Sensor, nameof(SensorTask.Interval), "Look Every");
                    if (((DetectionTask)Sensor).Sensors.Count == 0)
                        EditorGUILayout.HelpBox("Sees nothing: drag from the Sensors pin to an empty place and pick a sensor.",
                            MessageType.Warning);
                }

                ElementInspector.Fields(_document, Sensor, ownOnly: true);
                ElementInspector.Wired = false;
                ElementInspector.Compact = false;
                if (EditorGUI.EndChangeCheck())
                    _document.TouchQuietly();

                // Where what it sees goes: a variable of the blackboard, given by a wire — never made up by the sensor
                if (string.IsNullOrEmpty(Sensor.StoreIn))
                    EditorGUILayout.HelpBox("Writes nowhere. Drag an object variable from the Blackboard onto this sensor, " +
                                            "or wire the Object pin into a node of the variable.", MessageType.Warning);
                else
                    EditorGUILayout.LabelField("Writes to", Sensor.StoreIn);

                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        // ---------- what every node of the canvas shows: its name, its type under a label, its requirement ----------

        /// <summary>
        /// The node's title: the user's label, else its type (step 90: a C# class name never shows). A labelled node shows
        /// its type in small letters under the title, so what it is stays seen. A double click on the title renames it.
        /// </summary>
        private static void ShowTitle(Node view, AgentNode node, GraphDocument document)
        {
            view.title = NodeTitles.Shown(node);
            view.tooltip = NodeTitles.IsLabelled(node) ? $"{node.Label} — a {TypeTitleOf(node)}" : view.tooltip;
            if (NodeTitles.IsLabelled(node))
            {
                var caption = new Label(TypeTitleOf(node)) { name = TYPE_CAPTION, pickingMode = PickingMode.Ignore };
                caption.style.fontSize = 9;
                caption.style.color = new Color(0.68f, 0.68f, 0.68f);
                caption.style.paddingLeft = 8;
                caption.style.paddingBottom = 1;
                caption.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.95f);
                view.mainContainer.Insert(Mathf.Min(1, view.mainContainer.childCount), caption);
            }

            Label titleLabel = view.titleContainer.Q<Label>("title-label");
            titleLabel?.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount != 2 || evt.button != 0 || document == null || document.IsReadOnly)
                    return;

                evt.StopPropagation();
                view.GetFirstAncestorOfType<GraphViewCanvas>()?.BeginRename(view, node);
            });
        }

        /// <summary>The name of the small caption a labelled node shows its type in (tests find it by it).</summary>
        public const string TYPE_CAPTION = "type-caption";

        /// <summary>The name of the line that says what a node requires or does by itself (tests find it by it).</summary>
        public const string REQUIREMENT = "requirement";

        private static string TypeTitleOf(AgentNode node) => node is AgentState state ? NodeTitles.Of(state) : NodeTitles.Of(node);

        // What the block requires or does by itself (AgentTask.Requirement), or what a condition reads that no wire shows
        // (AgentCondition.Requirement: Attack Slot — "target: Brain's"), on the node under its settings: the canvas is the
        // program — what no wire shows is said here (step 90, spec 10.2, 4.8)
        private static void AddRequirement(Node view, AgentNode node)
        {
            string requirement = (node as AgentTask)?.Requirement ?? (node as AgentCondition)?.Requirement;
            if (string.IsNullOrEmpty(requirement))
                return;

            var line = new Label(requirement) { name = REQUIREMENT };
            line.style.whiteSpace = WhiteSpace.Normal;
            line.style.unityFontStyleAndWeight = FontStyle.Italic;
            line.style.color = new Color(0.86f, 0.74f, 0.42f);
            line.style.fontSize = 10;
            line.style.paddingLeft = line.style.paddingRight = 6;
            line.style.paddingTop = line.style.paddingBottom = 2;
            view.extensionContainer.Add(line);
        }

        // ---------- renaming a node on the canvas (C4) ----------

        /// <summary>
        /// The field a node's title turns into while it is renamed: Enter keeps the label, Esc drops the change, an empty
        /// label — the node is called by its type again. Commit and Cancel are what the keys do (tests call them).
        /// </summary>
        public sealed class NodeRenamer
        {
            private readonly GraphDocument _document;
            private readonly AgentNode _node;
            private readonly Label _title;
            private bool _done;

            internal NodeRenamer(GraphDocument document, Node view, AgentNode node)
            {
                _document = document;
                _node = node;
                _title = view.titleContainer.Q<Label>("title-label");
                Field = new TextField { name = "rename-field", value = NodeTitles.Shown(node), isDelayed = false };
                Field.style.minWidth = 120f;
                Field.style.flexGrow = 1;
                int at = _title != null ? view.titleContainer.IndexOf(_title) : 0;
                if (_title != null)
                    _title.style.display = DisplayStyle.None;
                view.titleContainer.Insert(Mathf.Max(0, at), Field);

                Field.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    {
                        evt.StopPropagation();
                        Commit();
                    }
                    else if (evt.keyCode == KeyCode.Escape)
                    {
                        evt.StopPropagation();
                        Cancel();
                    }
                }, TrickleDown.TrickleDown);
                Field.RegisterCallback<FocusOutEvent>(_ => Commit());
            }

            /// <summary>The field the label is typed in.</summary>
            public TextField Field { get; }

            /// <summary>The typed label is kept (empty or the type's title — the node is called by its type).</summary>
            public void Commit()
            {
                if (_done)
                    return;

                _done = true;
                string typed = Field.value;
                Close();
                _document.RenameNode(_node, typed);
            }

            /// <summary>The title stays as it was.</summary>
            public void Cancel()
            {
                if (_done)
                    return;

                _done = true;
                Close();
            }

            private void Close()
            {
                Field.RemoveFromHierarchy();
                if (_title != null)
                    _title.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>The title of the node of this element turns into a field to type its label in (null — no such node).</summary>
        public NodeRenamer BeginRename(AgentNode node)
        {
            Node view = ViewOf(node);
            return view != null ? BeginRename(view, node) : null;
        }

        private NodeRenamer BeginRename(Node view, AgentNode node)
        {
            if (_document == null || _document.IsReadOnly || view == null || node == null)
                return null;

            var renamer = new NodeRenamer(_document, view, node);
            renamer.Field.schedule.Execute(() =>
            {
                renamer.Field.Focus();
                renamer.Field.SelectAll();
            });
            return renamer;
        }

        // The node of the canvas an element is drawn as (a state, a sensor, a shape, a value node, a condition)
        private Node ViewOf(AgentNode node) => node switch
        {
            AgentState state when _nodes.TryGetValue(state.Id, out StateNode view) => view,
            SensorTask sensor when _sensors.TryGetValue(sensor, out SensorNode view) => view,
            SensorTask shape => graphElements.OfType<ShapeNode>().FirstOrDefault(view => view.Shape == shape),
            AgentValue value when _valueViews.TryGetValue(value, out ValueNodeView view) => view,
            AgentCondition condition => _conditionOwners.Keys.FirstOrDefault(view => view.Condition == condition),
            _ => null
        };

        /// <summary>
        /// Every wire of the canvas: drawn in one colour — its own (Tint: the colour of the pin it leaves), brighter
        /// and thicker when it belongs to the picked node, faded when another node is picked, flashing when the agent
        /// has just taken it (Play). GraphView repaints an edge by its pins on its own occasions, so the look is put
        /// back after each of them and, to be sure, a few times a second.
        /// </summary>
        private class WireEdge : Edge
        {
            private int _emphasis;
            private bool _flashing;
            private Color _flash;
            private int _flashWidth = 2;

            public WireEdge()
            {
                schedule.Execute(Apply).Every(300);
            }

            /// <summary>The wire's own colour.</summary>
            public Color Tint = new(0.78f, 0.78f, 0.78f);

            /// <summary>1 — a wire of the picked node, −1 — a wire of another one (faded), 0 — nothing is picked.</summary>
            public void SetEmphasis(int emphasis)
            {
                if (_emphasis == emphasis)
                    return;

                _emphasis = emphasis;
                Apply();
            }

            public void SetFlash(Color color, int width)
            {
                _flashing = true;
                _flash = color;
                _flashWidth = width;
                Apply();
            }

            public void ClearFlash()
            {
                if (!_flashing)
                    return;

                _flashing = false;
                Apply();
            }

            public override void OnSelected()
            {
                base.OnSelected();
                Apply();
            }

            public override void OnUnselected()
            {
                base.OnUnselected();
                Apply();
            }

            public override bool UpdateEdgeControl()
            {
                bool updated = base.UpdateEdgeControl();
                Apply();
                return updated;
            }

            private void Apply()
            {
                if (edgeControl == null)
                    return;

                Color color = selected ? selectedColor : Tint;
                int width = _emphasis > 0 ? 4 : 2;
                if (_flashing)
                {
                    color = _flash;
                    width = _flashWidth;
                }
                else if (_emphasis < 0 && !selected)
                {
                    color = Color.Lerp(Tint, FadeColor, 0.8f);
                }

                edgeControl.inputColor = color;
                edgeControl.outputColor = color;
                edgeControl.edgeWidth = width;
            }
        }

        /// <summary>
        /// A stretch of a wire that touches a wire point. A plain edge always leaves a pin to the right and enters one
        /// from the left, which draws a hook at a point that has no sides; this one goes straight to the point.
        /// </summary>
        private sealed class KnotEdge : WireEdge
        {
            protected override EdgeControl CreateEdgeControl() => new KnotEdgeControl { capRadius = 4f, interceptWidth = 6f };
        }

        private sealed class KnotEdgeControl : EdgeControl
        {
            private const float CORNER = 8f;
            private const float MIN_RUN = 24f;
            private const int CORNER_STEPS = 6;

            // The polyline an edge is drawn (and hit-tested) by. The base fills it with rounded corners around two
            // stubs and offers no other way in; a stretch at a wire point needs no stubs, so it is filled here
            private static readonly System.Reflection.FieldInfo RenderPointsField = typeof(EdgeControl).GetField(
                "m_RenderPoints", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            /// <summary>The stretch starts at a wire point (not at a pin of a node).</summary>
            public bool FromKnot;

            /// <summary>The stretch ends at a wire point.</summary>
            public bool ToKnot;

            /// <summary>The stretch ends at a link label docked right by its pin: drawn at right angles however short.</summary>
            public bool Short;

            /// <summary>
            /// A wire of the Brain (step 90, C10): the corners it runs through, in the canvas's content, from its point on the
            /// Brain's frame to the decision's In pin (GraphLayout.BrainWirePath); null — drawn pin to pin.
            /// </summary>
            public List<Vector2> FramePath;

            // A wire runs level and straight up or down, never slanted. Pin to pin — level out of the pin, down (or
            // up) halfway, level into the other pin. Pin to a wire point — level to above the point, then down to it;
            // a wire point to a pin — the same the other way round. Point to point — level, then down. A wire that
            // has to go backwards (its end is to the left of its start) keeps the editor's own shape. A wire of the
            // Brain runs from its own point on the Brain's frame, along its own lane
            protected override void UpdateRenderPoints()
            {
                ComputeControlPoints();
                Vector2[] points = controlPoints;
                GraphView view = GetFirstAncestorOfType<GraphView>();
                if (points == null || points.Length != 4 || view == null ||
                    RenderPointsField?.GetValue(this) is not List<Vector2> render)
                {
                    base.UpdateRenderPoints();          // the editor changed inside: the plain look is better than none
                    return;
                }

                if (FramePath is { Count: >= 2 })
                {
                    render.Clear();
                    render.Add(Local(view, FramePath[0]));
                    for (int i = 1; i < FramePath.Count - 1; i++)
                        AddCorner(render, Local(view, FramePath[i - 1]), Local(view, FramePath[i]), Local(view, FramePath[i + 1]));
                    render.Add(Local(view, FramePath[^1]));
                    return;
                }

                Vector2 from = Local(view, points[0]);
                Vector2 to = Local(view, points[3]);
                bool level = Mathf.Abs(from.y - to.y) < 1f;
                bool upright = Mathf.Abs(from.x - to.x) < 1f;
                bool pinToPin = !FromKnot && !ToKnot;
                if (pinToPin && to.x < from.x + (Short && to.x > from.x ? 0f : MIN_RUN))
                {
                    base.UpdateRenderPoints();          // backwards: around the nodes, the way the editor draws it
                    return;
                }

                render.Clear();
                render.Add(from);
                if (level || upright)
                {
                    // already straight: nothing to turn
                }
                else if (pinToPin)
                {
                    float middle = (from.x + to.x) * 0.5f;
                    var first = new Vector2(middle, from.y);
                    var second = new Vector2(middle, to.y);
                    AddCorner(render, from, first, second);
                    AddCorner(render, first, second, to);
                }
                else if (FromKnot && !ToKnot)
                {
                    // into a pin: a pin is entered from its left, level
                    AddCorner(render, from, from.x < to.x - 1f ? new Vector2(from.x, to.y) : Local(view, points[2]), to);
                }
                else
                {
                    // out of a pin (level, to the right), or from a point to a point: level first, then down
                    bool reachable = FromKnot || to.x > from.x + 1f;
                    AddCorner(render, from, reachable ? new Vector2(to.x, from.y) : Local(view, points[1]), to);
                }

                render.Add(to);
            }

            private Vector2 Local(GraphView view, Vector2 point) =>
                view.contentViewContainer.ChangeCoordinatesTo(this, point);

            // The corner between a→corner and corner→b, rounded: a short curve that starts and ends on the two lines
            private static void AddCorner(List<Vector2> render, Vector2 a, Vector2 corner, Vector2 b)
            {
                Vector2 toA = a - corner;
                Vector2 toB = b - corner;
                float radius = Mathf.Min(CORNER, toA.magnitude * 0.5f, toB.magnitude * 0.5f);
                if (radius < 0.5f)
                {
                    render.Add(corner);                 // no room to round (or the three lie on one spot)
                    return;
                }

                Vector2 start = corner + toA.normalized * radius;
                Vector2 end = corner + toB.normalized * radius;
                for (int i = 0; i <= CORNER_STEPS; i++)
                {
                    float t = i / (float)CORNER_STEPS;
                    Vector2 first = Vector2.Lerp(start, corner, t);
                    Vector2 second = Vector2.Lerp(corner, end, t);
                    render.Add(Vector2.Lerp(first, second, t));
                }
            }
        }


        /// <summary>
        /// A point a wire is bent through, drawn as one dot (a reroute knot): made by a double click on the wire,
        /// dragged like a node, deleted with Delete. It only shapes the wire — nothing can be plugged into it.
        /// The wire comes in and goes out at the middle of the dot: its two pins lie on top of each other, unseen.
        /// </summary>
        private sealed class WirePoint : CanvasNode
        {
            public static readonly Vector2 Size = new(14f, 14f);

            public WirePoint(object wire, int index, Type portType, Color color)
            {
                Wire = wire;
                Index = index;
                capabilities &= ~(Capabilities.Copiable | Capabilities.Renamable | Capabilities.Collapsible);

                // Nothing of a node is drawn: no title, no body — only the dot
                mainContainer.style.display = DisplayStyle.None;
                style.minWidth = 0f;
                style.minHeight = 0f;
                style.width = Size.x;
                style.height = Size.y;

                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.style.position = Position.Absolute;
                dot.style.left = dot.style.top = dot.style.right = dot.style.bottom = 0f;
                dot.style.backgroundColor = color;
                dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius =
                    dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = Size.x * 0.5f;
                Add(dot);

                In = Make(Direction.Input, portType, color);
                Out = Make(Direction.Output, portType, color);
                Add(In);
                Add(Out);
            }

            public object Wire { get; }

            public int Index { get; }

            public Port In { get; }

            public Port Out { get; }

            // A pin that fills the whole dot with its connector in the middle, and is not seen
            private Port Make(Direction direction, Type portType, Color color)
            {
                Port port = InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Single, portType);
                port.portName = "";
                port.portColor = color;
                if (port.edgeConnector != null)
                    port.RemoveManipulator(port.edgeConnector);     // a point is not a place to start a wire from

                Label label = port.Q<Label>();
                if (label != null)
                    label.style.display = DisplayStyle.None;

                port.style.position = Position.Absolute;
                port.style.left = port.style.top = 0f;
                port.style.width = port.style.minWidth = port.style.maxWidth = Size.x;
                port.style.height = port.style.minHeight = port.style.maxHeight = Size.y;
                port.style.paddingLeft = port.style.paddingRight = port.style.paddingTop = port.style.paddingBottom = 0f;
                port.style.marginLeft = port.style.marginRight = port.style.marginTop = port.style.marginBottom = 0f;

                // A wire ends at the middle of the connector of a pin: the connector is stretched over the whole dot, so
                // that middle is the middle of the dot (by default it sits at one side of a pin 24 px high)
                VisualElement connector = port.Q("connector");
                if (connector != null)
                {
                    connector.style.position = Position.Absolute;
                    connector.style.left = connector.style.top = connector.style.right = connector.style.bottom = 0f;
                    connector.style.marginLeft = connector.style.marginRight =
                        connector.style.marginTop = connector.style.marginBottom = 0f;
                    connector.style.width = Size.x;
                    connector.style.height = Size.y;
                }

                port.style.opacity = 0f;

                // The mouse must reach the dot itself, not the pins on top of it: a pin is a graph element of its own,
                // and a click that lands on it selects and drags nothing
                port.pickingMode = PickingMode.Ignore;
                port.Query<VisualElement>().ForEach(part => part.pickingMode = PickingMode.Ignore);
                return port;
            }
        }

        /// <summary>
        /// A variable on the canvas, as a small node: the arrow in comes from the node that fills the variable (a
        /// sensor), the arrows out go to the nodes that read it. A variable may have several such nodes (Duplicate in
        /// the right-click menu) — each node of the graph is wired to the nearest one.
        /// </summary>
        private sealed class VariableNodeView : CanvasNode
        {
            public static readonly Vector2 Size = new(130f, 28f);

            private readonly Type _pinType;

            public VariableNodeView(GraphVariableNode data, Type pinType)
            {
                _pinType = pinType;
                Data = data;
                title = data.Variable;
                titleContainer.style.backgroundColor = new Color(0.12f, 0.32f, 0.42f);
                titleContainer.style.height = 22f;
                tooltip = "Ctrl+D: one more node of this variable";
                capabilities &= ~(Capabilities.Renamable | Capabilities.Collapsible);       // copied with Ctrl+D
                titleButtonContainer.style.display = DisplayStyle.None;      // no collapse arrow: there is nothing to fold
                In = Make(Direction.Input, Port.Capacity.Single, FillColor);     // orange: what fills the variable
                Out = Make(Direction.Output, Port.Capacity.Multi, TypeColor(pinType));    // what reads it: by its type
                if (In.edgeConnector != null)
                    In.RemoveManipulator(In.edgeConnector);      // the sensor fills it: not rewired by hand
                titleContainer.Insert(0, In);
                titleContainer.Add(Out);
                style.minWidth = 0f;
                RefreshExpandedState();
            }

            public GraphVariableNode Data { get; }

            public Port In { get; }

            public Port Out { get; }

            private Port Make(Direction direction, Port.Capacity capacity, Color color)
            {
                Port port = InstantiatePort(Orientation.Horizontal, direction, capacity, _pinType);
                port.portName = "";
                port.portColor = color;
                return port;
            }
        }

        /// <summary>
        /// The end of an arrow shown as a link: a compact label "→ node" the wire comes into instead of running across the
        /// canvas to the node itself (step 90, C2): docked at its output pin (stacked under the other labels of the pin)
        /// unless dragged away; "Dock To Pin" puts it back. The arrow is the same arrow — only drawn short. A double click
        /// goes to the node; Delete deletes the arrow.
        /// </summary>
        private sealed class LinkNode : CanvasNode
        {
            /// <summary>The label's height (a pin is 24 px high: the first label never reaches the next pin's row).</summary>
            public const float HEIGHT = 18f;

            private readonly Label _text;

            public LinkNode(Transition transition, string targetName, Color color, bool docked, Action open)
            {
                Transition = transition;
                title = $"→ {targetName}";
                tooltip = "The arrow leads to this node. Click: show it; double click: go to it. Drag it off its pin to " +
                          "put it elsewhere";
                capabilities &= ~(Capabilities.Copiable | Capabilities.Renamable | Capabilities.Collapsible);

                // Nothing of a node is drawn: one pill as wide as its text, its pin at its left edge
                mainContainer.style.display = DisplayStyle.None;
                style.minWidth = 0f;
                style.minHeight = 0f;
                var pill = new VisualElement { name = "link-pill" };
                pill.style.flexDirection = FlexDirection.Row;
                pill.style.alignItems = Align.Center;
                pill.style.height = HEIGHT;
                pill.style.backgroundColor = LinkColor;
                pill.style.borderTopLeftRadius = pill.style.borderTopRightRadius =
                    pill.style.borderBottomLeftRadius = pill.style.borderBottomRightRadius = HEIGHT * 0.5f;
                Add(pill);

                In = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(Transition));
                In.portName = "";
                In.portColor = color;
                if (In.edgeConnector != null)
                    In.RemoveManipulator(In.edgeConnector);      // the end of one arrow: nothing else is plugged in
                In.style.height = HEIGHT;
                In.style.paddingLeft = In.style.paddingRight = 0f;
                In.style.width = 14f;
                In.style.minWidth = 14f;
                pill.Add(In);

                _text = new Label(title) { name = "link-text", pickingMode = PickingMode.Ignore };
                _text.style.color = Color.white;
                _text.style.fontSize = 11;
                _text.style.paddingRight = 7;
                _text.style.unityTextAlign = TextAnchor.MiddleLeft;
                pill.Add(_text);
                SetDocked(docked);

                RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.clickCount != 2 || evt.button != 0)
                        return;

                    evt.StopPropagation();
                    schedule.Execute(open);
                });
            }

            public Transition Transition { get; }

            public Port In { get; }

            /// <summary>The label stands at its pin (moved with its node), not where it was dragged.</summary>
            public bool IsDocked { get; private set; }

            /// <summary>The label's size, as near as it is known before it is laid out (its text measured roughly).</summary>
            public Vector2 Size => new(Mathf.Max(60f, 22f + 6.5f * title.Length), HEIGHT);

            public void SetDocked(bool docked)
            {
                IsDocked = docked;
                _text.style.unityFontStyleAndWeight = docked ? FontStyle.Normal : FontStyle.Italic;
            }
        }

        /// <summary>The wire from a condition node into what asks it: deleting the wire takes the condition away.</summary>
        private sealed class ConditionWire
        {
            public AgentCondition Condition;
        }

        private readonly Dictionary<ConditionNode, StateNode> _conditionOwners = new();

        // The conditions are nodes of their own: a value of the blackboard ("Ammo is at least 3", "Alarm is on"), and the
        // operators that join them (AND, OR; "not" is a switch of any of them). The answer goes by a wire into a
        // Condition pin — of a Branch, of a decision (step 90): no arrow has one, the canvas is the program

        private void ShowConditions(GraphDocument document)
        {
            _conditionOwners.Clear();
            foreach (StateNode node in _nodes.Values.ToList())
            {
                Vector2 at = document.PositionOf(node.State);
                foreach (Port port in node.ConditionPorts)
                {
                    var slot = (ConditionSlot)port.userData;
                    if (slot.Owner is AgentNode asker && GraphDocument.ConditionLinkOf(asker, slot.Field) is { } link &&
                        link.Field.GetValue(asker) is AgentCondition asked)
                        ShowCondition(document, asked, port, node, at + new Vector2(-40f, -150f));
                }
            }
        }

        private void ShowCondition(GraphDocument document, AgentCondition condition, Port into, StateNode owner, Vector2 fallback)
        {
            var view = new ConditionNode(document, condition);
            Vector2 place = document.PositionOfNode(condition);
            view.SetPosition(new Rect(place == Vector2.zero ? fallback : place, new Vector2(200f, 60f)));
            AddElement(view);
            _conditionOwners[view] = owner;
            Link(view.Out, into, new ConditionWire { Condition = condition }, dashed: false);
            if (view.VariablePort != null && condition is CompareCondition compare)
            {
                GraphVariableNode feeder = string.IsNullOrEmpty(compare.Variable) ? null : document.WiredNode(compare, compare.Variable);
                if (feeder != null && _variables.TryGetValue(feeder, out VariableNodeView source))
                    LinkWire(source.Out, view.VariablePort, view.VariablePort.userData, compare);

                GraphVariableNode other = view.OtherPort == null || string.IsNullOrEmpty(compare.Other)
                    ? null
                    : document.Asset.VariableNodes.Find(node => node.Variable == compare.Other && node.Readers.Contains(compare.Id));
                if (other != null && _variables.TryGetValue(other, out VariableNodeView against))
                    LinkWire(against.Out, view.OtherPort, view.OtherPort.userData, view.OtherPort);
            }

            // The objects of a fact condition, each from the node of its variable the wire was drawn from
            LinkVariables(document, view.DataPorts);

            List<AgentCondition> inputs = GraphDocument.InputsOf(condition);
            for (int i = 0; inputs != null && i < inputs.Count; i++)
                ShowCondition(document, inputs[i], view.In, owner, view.GetPosition().position + new Vector2(-240f, i * 110f - 40f));
        }

        // Each object pin from the node of its variable the wire was drawn from
        private void LinkVariables(GraphDocument document, IEnumerable<Port> pins)
        {
            foreach (Port port in pins)
            {
                var link = (DataLink)port.userData;
                string variable = ValueFields.VariableOf(link.Owner, link.Field);
                GraphVariableNode feeder = string.IsNullOrEmpty(variable) ? null : document.WiredNode(link.Owner, variable);
                if (feeder != null && _variables.TryGetValue(feeder, out VariableNodeView source))
                    LinkWire(source.Out, port, link, link.Owner);
            }
        }

        // ---------- value nodes (step 90): data a node takes by a wire — a Get, a What, an Ability ----------

        private readonly Dictionary<AgentValue, ValueNodeView> _valueViews = new();

        // The output pins of blocks by the value each gives out (step 91: Component and Found of Try Get Component)
        private readonly Dictionary<AgentValue, Port> _givenBy = new();

        // An output data pin of a block: the value it gives out
        private sealed class ValueSource
        {
            public AgentValue Value;
        }

        // The value a pin gives out: a value node's, or a block's output pin's (null — the pin gives no value)
        private static AgentValue GivenAt(Port port) =>
            port?.node is ValueNodeView view && port == view.Out ? view.Value : port?.userData is ValueSource source ? source.Value : null;

        // The colour of a value's pin and wire by what it gives
        private static Color ValueKindColor(ValueKind kind) => kind switch
        {
            ValueKind.Number => TypeColor(typeof(float)),
            ValueKind.Flag => TypeColor(typeof(bool)),
            ValueKind.Object => TypeColor(typeof(Transform)),
            ValueKind.Filter => new Color(0.95f, 0.62f, 0.25f),
            ValueKind.Ability => new Color(0.75f, 0.48f, 0.95f),
            _ => DataColor
        };

        private static readonly Dictionary<Type, Color> SlotColors = new();

        // The colour of a value pin by the kind of value it takes: the one kind every value node it takes gives
        private static Color SlotColor(Type taken)
        {
            if (SlotColors.TryGetValue(taken, out Color color))
                return color;

            ValueKind? only = null;
            bool mixed = false;
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (!taken.IsAssignableFrom(entry.Type) || AIKitNodeRegistry.Create(entry.Info.Id) is not AgentValue value)
                    continue;

                mixed |= only != null && only != value.Kind;
                only ??= value.Kind;
            }

            color = only != null && !mixed ? ValueKindColor(only.Value) : DataColor;
            SlotColors[taken] = color;
            return color;
        }

        // The value pins of a node: one per value link (a list link takes several wires)
        private static List<Port> AddValuePorts(Node view, AgentNode owner, VisualElement into)
        {
            var pins = new List<Port>();
            foreach (GraphFields.Link link in ValueFields.ValueLinks(owner))
            {
                Port port = view.InstantiatePort(Orientation.Horizontal, Direction.Input,
                    link.IsList ? Port.Capacity.Multi : Port.Capacity.Single, typeof(AgentValue));
                port.portName = ObjectNames.NicifyVariableName(link.Name);
                port.portColor = SlotColor(link.ElementType);
                port.userData = new ValueSlot { Owner = owner, Field = link.Name, Type = link.ElementType, IsList = link.IsList };
                port.tooltip = link.IsList
                    ? "Wire value nodes here — any number of them (let go on an empty place: the list of what fits)"
                    : "Wire a value node here (let go on an empty place: the list of what fits)";
                into.Add(port);
                pins.Add(port);
            }

            return pins;
        }

        // Every value node of the graph — read by a node or not yet — and the wires into the pins that read them
        private void ShowValues(GraphDocument document)
        {
            List<AgentValue> values = document.Values();
            for (int i = 0; i < values.Count; i++)
            {
                AgentValue value = values[i];
                if (value is IBlockOutput)
                    continue;                           // a block's output: drawn as the block's output pin
                if (!_shownIds.Contains(value.Id) && !(_scope == null && document.Graph.Root.Values.Contains(value)))
                    continue;                           // read in another view (a subgraph, or the graph)

                var view = new ValueNodeView(document, value);
                Vector2 place = document.PositionOfNode(value);
                if (place == Vector2.zero)
                    place = PlaceNearReader(document, value, i);
                view.SetPosition(new Rect(place, ValueNodeView.Size));
                AddElement(view);
                _valueViews[value] = view;
            }

            foreach (StateNode node in _nodes.Values)
            {
                foreach (Port given in node.ValueOutPorts)
                    _givenBy[((ValueSource)given.userData).Value] = given;
            }

            var readers = new List<(Port Pin, ValueSlot Slot)>();
            foreach (StateNode node in _nodes.Values)
                readers.AddRange(node.ValuePorts.Select(pin => (pin, (ValueSlot)pin.userData)));
            foreach (ConditionNode node in _conditionOwners.Keys)
                readers.AddRange(node.ValuePorts.Select(pin => (pin, (ValueSlot)pin.userData)));
            foreach (ValueNodeView node in _valueViews.Values)
                readers.AddRange(node.ValuePorts.Select(pin => (pin, (ValueSlot)pin.userData)));

            foreach ((Port pin, ValueSlot slot) in readers)
            {
                GraphFields.Link link = GraphFields.Find(slot.Owner.GetType(), slot.Field);
                if (link == null)
                    continue;

                foreach (object held in link.Values(slot.Owner))
                {
                    Port source = held is AgentValue given && _valueViews.TryGetValue(given, out ValueNodeView from) ? from.Out
                        : held is AgentValue output && _givenBy.TryGetValue(output, out Port pinOut) ? pinOut
                        : null;
                    if (source != null)
                        Link(source, pin, new ValueWire { Value = (AgentValue)held, Reader = slot.Owner, Field = slot.Field }, dashed: false);
                }
            }

            foreach (ValueNodeView view in _valueViews.Values)
                LinkVariables(document, view.DataPorts);
        }

        // A value node with no place of its own (made in code): left of the first node that reads it
        private Vector2 PlaceNearReader(GraphDocument document, AgentValue value, int index)
        {
            foreach ((AgentNode reader, GraphFields.Link _) in document.ReadersOf(value))
            {
                // Not laid out yet: where the document puts the reader
                StateNode holder = reader is AgentTask task
                    ? _nodes.Values.FirstOrDefault(node => node.State.Tasks.Contains(task))
                    : null;
                Vector2 at = holder != null ? document.PositionOf(holder.State) : document.PositionOfNode(reader);
                return at + new Vector2(-230f, 40f);
            }

            return new Vector2(-400f, 300f + index * 90f);
        }

        /// <summary>
        /// A value node (step 90): a small node of data — its title, its own settings, the pins of what it reads (an
        /// object by a wire from a variable node, another value) and one output pin coloured by what it gives. Wired into
        /// any number of value pins; with no wire it stays on the canvas (kept with the graph).
        /// </summary>
        private sealed class ValueNodeView : CanvasNode
        {
            public static readonly Vector2 Size = new(190f, 60f);

            private readonly GraphDocument _document;

            public ValueNodeView(GraphDocument document, AgentValue value)
            {
                _document = document;
                Value = value;
                ShowTitle(this, value, document);
                titleContainer.style.backgroundColor = NodeKinds.TitleColor(value.GetType()) ?? DataColor;
                titleContainer.style.height = 22f;
                titleButtonContainer.style.display = DisplayStyle.None;
                capabilities &= ~(Capabilities.Renamable | Capabilities.Collapsible | Capabilities.Copiable);

                Out = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(AgentValue));
                Out.portName = "";
                Out.portColor = ValueKindColor(value.Kind);
                Out.tooltip = $"Gives {value.Kind.ToString().ToLowerInvariant()}: wire it into the pins that read it";
                titleContainer.Add(Out);

                // What it reads: an object by a wire from a node of its variable, a number in its pin, another value
                foreach (System.Reflection.FieldInfo field in ValueFields.Pins(value))
                {
                    Port port = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single,
                        ValueFields.PinType(field));
                    port.portName = ObjectNames.NicifyVariableName(field.Name);
                    port.portColor = TypeColor(port.portType);
                    port.userData = new DataLink { Owner = value, Field = field };
                    if (ValueFields.IsValue(field))
                        AddPinField(port, value, field, document);
                    inputContainer.Add(port);
                    DataPorts.Add(port);
                }

                ValuePorts = AddValuePorts(this, value, inputContainer);

                var body = new IMGUIContainer(Draw);
                body.style.paddingLeft = body.style.paddingRight = 6;
                body.style.paddingTop = body.style.paddingBottom = 3;
                body.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                extensionContainer.Add(body);
                style.minWidth = 170f;
                RefreshPorts();
                RefreshExpandedState();
            }

            public AgentValue Value { get; }

            public Port Out { get; }

            /// <summary>The pins of the objects and numbers it reads (each fed by a wire from a node of its variable).</summary>
            public List<Port> DataPorts { get; } = new();

            /// <summary>The pins of the values it reads.</summary>
            public List<Port> ValuePorts { get; }

            private void Draw()
            {
                float labelWidth = EditorGUIUtility.labelWidth;
                ElementInspector.Compact = true;
                ElementInspector.Wired = true;
                ElementInspector.ValuesInPins = true;
                EditorGUIUtility.labelWidth = ElementInspector.LabelWidthFor(Value, ownOnly: true);
                EditorGUI.BeginChangeCheck();
                ElementInspector.Fields(_document, Value, ownOnly: true);
                ElementInspector.ValuesInPins = false;
                ElementInspector.Wired = false;
                ElementInspector.Compact = false;
                if (EditorGUI.EndChangeCheck())
                    _document.TouchQuietly();
                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        /// <summary>
        /// A condition on the canvas: a small node with its answer on the right. A fact or a comparison shows its
        /// own settings; AND and OR take their inputs on the left. "not" turns the answer over.
        /// </summary>
        private sealed class ConditionNode : CanvasNode
        {
            private readonly GraphDocument _document;

            public ConditionNode(GraphDocument document, AgentCondition condition)
            {
                _document = document;
                Condition = condition;
                bool isOperator = GraphDocument.InputsOf(condition) != null;
                bool isFlag = IsFlag(condition);
                title = TitleOf(condition);
                titleContainer.style.backgroundColor = isOperator ? new Color(0.5f, 0.38f, 0.1f)
                    : isFlag ? new Color(0.45f, 0.1f, 0.1f) : new Color(0.1f, 0.36f, 0.34f);
                titleContainer.style.height = 22f;
                titleButtonContainer.style.display = DisplayStyle.None;
                capabilities &= ~(Capabilities.Renamable | Capabilities.Collapsible | Capabilities.Copiable);

                Out = Pin(Direction.Output, Port.Capacity.Single);
                outputContainer.Add(Out);
                if (isOperator)
                {
                    In = Pin(Direction.Input, Port.Capacity.Multi);
                    In.userData = new ConditionSlot { Owner = condition };
                    inputContainer.Add(In);
                }

                if (condition is CompareCondition asked && !isFlag)
                {
                    // The variable to compare comes by a wire from a node of that variable — unless a value node (a Get)
                    // is wired into A: that is what is compared then, and A has one pin, its own
                    if (asked.A == null)
                    {
                        VariablePort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single,
                            ValueFields.PinType(asked.Kind));
                        VariablePort.portName = "A";
                        VariablePort.portColor = TypeColor(VariablePort.portType);
                        VariablePort.userData = new DataLink { Owner = asked, Field = typeof(CompareCondition).GetField("Variable") };
                        inputContainer.Add(VariablePort);
                    }

                    // A number is compared with B: a number typed in the pin, or another variable wired in (a value node in
                    // B takes its place)
                    if (IsNumberCompare(asked) && asked.B == null)
                    {
                        OtherPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(float));
                        OtherPort.portName = "B";
                        OtherPort.portColor = TypeColor(typeof(float));
                        OtherPort.userData = new DataLink { Owner = asked, Field = typeof(CompareCondition).GetField("Other") };
                        if (string.IsNullOrEmpty(asked.Other))
                        {
                            var limit = new FloatField { value = asked.Value };
                            limit.style.width = 44f;
                            limit.style.marginLeft = 2f;
                            limit.RegisterValueChangedCallback(change =>
                            {
                                asked.Value = change.newValue;
                                document.TouchQuietly();
                            });
                            OtherPort.Add(limit);
                        }

                        inputContainer.Add(OtherPort);
                    }
                }
                else if (!isOperator)
                {
                    // A fact about an object ("Grenade Is Ready" for Target): the object comes by a wire into its pin,
                    // as into a task — unplugged, the condition has no object to ask about
                    foreach (System.Reflection.FieldInfo field in ValueFields.Pins(condition))
                    {
                        Port port = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single,
                            ValueFields.PinType(field));
                        port.portName = ObjectNames.NicifyVariableName(field.Name);
                        port.portColor = TypeColor(port.portType);
                        port.userData = new DataLink { Owner = condition, Field = field };
                        inputContainer.Add(port);
                        DataPorts.Add(port);
                    }
                }

                // The values it asks about (step 90: a Get of the target's health, a What), each by a wire into its pin
                ValuePorts = AddValuePorts(this, condition, inputContainer);

                if (isFlag)
                {
                    // Nothing to set: the name of the flag and the pin its value leaves by
                    titleContainer.Add(Out);
                    style.minWidth = 120f;
                }
                else
                {
                    var body = new IMGUIContainer(Draw);
                    body.style.paddingLeft = body.style.paddingRight = 6;
                    body.style.paddingBottom = 2;
                    extensionContainer.Add(body);
                    style.minWidth = isOperator || condition is CompareCondition ? 90f : 200f;
                    if (condition is CompareCondition)
                    {
                        body.style.paddingLeft = body.style.paddingRight = 3;
                        titleContainer.style.height = 18f;
                    }

                    AddRequirement(this, condition);       // what it reads without a wire ("target: Brain's")
                }

                RefreshPorts();
                RefreshExpandedState();
            }

            public AgentCondition Condition { get; }

            /// <summary>The pin a Compare takes its variable through (null — not a Compare).</summary>
            public Port VariablePort { get; }

            /// <summary>The object pins of a fact condition (its Transform variable fields), each fed by a wire.</summary>
            public List<Port> DataPorts { get; } = new();

            /// <summary>The pins of the values it asks about.</summary>
            public List<Port> ValuePorts { get; }

            /// <summary>The pin a Compare of numbers takes what it compares with (null — none).</summary>
            public Port OtherPort { get; }

            public Port Out { get; }

            public Port In { get; }

            // A flag of the blackboard that is asked "is it on": drawn as the variable itself (never one with a value in
            // A — that compares a number, whatever its Kind says)
            private static bool IsFlag(AgentCondition condition) =>
                condition is CompareCondition { Kind: VariableType.Bool, Expected: true, A: null } flag &&
                !string.IsNullOrEmpty(flag.Variable);

            // A Compare of numbers: a value node in A, or a number variable (CompareCondition.Answer compares so)
            private static bool IsNumberCompare(CompareCondition compare) =>
                compare.A != null
                    ? compare.A.Kind != ValueKind.Flag          // a flag wired into A (Found): "is on", no B
                    : compare.Kind != VariableType.Bool && compare.Kind != VariableType.Transform;

            // What an input of a Compare shows: the value node wired in (its phrase), else the variable
            private static string InputName(AgentValue value, string variable) =>
                value is GetValue get ? get.Phrase : value is FoundOutput found ? found.Phrase
                : value != null ? NodeTitles.Shown(value) : variable;

            // A flag shows its variable; any other condition its title (the user's label, else its type: "Compare", "AND")
            private static string TitleOf(AgentCondition condition)
            {
                return IsFlag(condition) ? ((CompareCondition)condition).Variable : NodeTitles.Shown(condition);
            }

            // Wired by the menu of the node that asks, not by hand: the pins only show where the answer goes
            private Port Pin(Direction direction, Port.Capacity capacity)
            {
                Port port = InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(bool));
                port.portName = "";
                port.portColor = ConditionColor;
                if (direction == Direction.Output && port.edgeConnector != null)
                    port.RemoveManipulator(port.edgeConnector);      // the answer leaves by the wire the list made
                return port;
            }

            // Compare: the variable, then how to compare — by what the variable is
            private static readonly string[] NumberWays = { "<", "\u2264", ">", "\u2265" };
            private static readonly string[] FlagWays = { "is on", "is off" };
            private static readonly string[] ObjectWays = { "is there", "is not there" };

            private void DrawCompare(CompareCondition compare)
            {
                // One row: the variable that came by the wire, how it is compared, with what
                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    string a = InputName(compare.A, compare.Variable);
                    bool none = string.IsNullOrEmpty(a);
                    var name = new GUIContent(none ? "variable?" : a,
                        none ? "Drag a variable from the Blackboard and wire it into the pin" : "");
                    GUILayout.Label(name, none ? EditorStyles.centeredGreyMiniLabel : EditorStyles.label,
                        GUILayout.Width(Mathf.Clamp(EditorStyles.label.CalcSize(name).x + 4f, 40f, 150f)));

                    if (!IsNumberCompare(compare))
                    {
                        string[] ways = compare.Kind == VariableType.Bool ? FlagWays : ObjectWays;
                        compare.Expected = EditorGUILayout.Popup(compare.Expected ? 0 : 1, ways,
                            GUILayout.Width(compare.Kind == VariableType.Bool ? 52f : 88f)) == 0;
                    }
                    else
                    {
                        compare.Comparison = (NumberComparison)EditorGUILayout.Popup((int)compare.Comparison, NumberWays, GUILayout.Width(34f));
                        string b = InputName(compare.B, compare.Other);
                        GUILayout.Label(string.IsNullOrEmpty(b) ? "B" : b, GUILayout.ExpandWidth(false));
                    }
                }

                if (EditorGUI.EndChangeCheck())
                    _document.TouchQuietly();
            }

            private void Draw()
            {
                if (Condition is CompareCondition compare)
                {
                    DrawCompare(compare);
                    return;
                }

                float labelWidth = EditorGUIUtility.labelWidth;
                ElementInspector.Compact = true;
                ElementInspector.Wired = DataPorts.Count > 0;      // its objects come only by the wires into its pins
                EditorGUIUtility.labelWidth = Mathf.Max(80f, ElementInspector.LabelWidthFor(Condition, ownOnly: true));
                EditorGUI.BeginChangeCheck();
                ElementInspector.Fields(_document, Condition, ownOnly: true);
                ElementInspector.Compact = false;
                ElementInspector.Wired = false;
                if (EditorGUI.EndChangeCheck())
                {
                    title = TitleOf(Condition);
                    _document.TouchQuietly();
                }

                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        /// <summary>The mark of a Random node's output pin: the number of the output.</summary>
        private sealed class RandomPin
        {
            public int Index;
        }

        private sealed class DataLink
        {
            public AgentNode Owner;
            public System.Reflection.FieldInfo Field;
        }

        private sealed class LiveEdge
        {
            public Edge Edge;
            public Transition Transition;
            public bool Lit;
        }

        /// <summary>A state on the canvas.</summary>
        private sealed class StateNode : CanvasNode
        {
            private readonly GraphDocument _document;
            private readonly Label _status;
            private readonly Port _success;
            private readonly Port _tick;
            private readonly Port _failure;
            private readonly Port _finished;
            private readonly Dictionary<Transition, Port> _ownPorts = new();
            private readonly Dictionary<int, Port> _outputPorts = new();
            private bool _isActive;
            private int _shownTenths = int.MinValue;

            public StateNode(GraphDocument document, AgentState state, bool isRoot, bool withHierarchy)
            {
                _document = document;
                State = state;
                // Called by its type ("Wait", "Strike") unless the user gave it a label (its type is shown under it then)
                if (isRoot)
                    title = $"{state.DisplayName} (root)";
                else
                    ShowTitle(this, state, document);

                // The colour of the title says what kind of node it is, at a glance (by what it is, not its path)
                if (TitleColor(state) is { } tint)
                    titleContainer.style.backgroundColor = tint;

                // What the node does, at a glance: a badge with a pictogram before its name
                VisualElement icon = NodeIcons.For(state);
                if (icon != null)
                    titleContainer.Insert(0, icon);
                if (isRoot)
                    capabilities &= ~Capabilities.Deletable;

                // The flow stays in this node, round and round: its task repeats (Attack: blow after blow) or never
                // ends (Patrol Area). A loop sign after the name says so
                if (HoldsTheFlow(state))
                {
                    var loop = new Label("↻")
                    {
                        tooltip = "The flow stays here for a while, think after think: the node lasts over time. It is " +
                                  "left by its pins or when the Brain takes another decision"
                    };
                    loop.style.fontSize = 15;
                    loop.style.unityFontStyleAndWeight = FontStyle.Bold;
                    loop.style.color = new Color(1f, 0.85f, 0.35f);
                    loop.style.marginLeft = 4;
                    loop.style.unityTextAlign = TextAnchor.MiddleLeft;
                    titleContainer.Insert(Mathf.Min(titleContainer.childCount, icon != null ? 2 : 1), loop);
                }

                _status = new Label();
                _status.style.unityTextAlign = TextAnchor.MiddleRight;
                _status.style.marginRight = 6;
                _status.style.color = ActiveColor;
                titleContainer.Add(_status);

                In = Add(Direction.Input, Port.Capacity.Multi, typeof(Transition), "In");
                if (state.IsBrain)
                    In.style.display = DisplayStyle.None;   // the Brain is where the graph starts: nothing leads in

                // An object a task works with comes in through a data pin: a wire from a sensor's "Object"
                foreach (AgentTask task in state.Tasks)
                {
                    foreach (System.Reflection.FieldInfo field in ValueFields.Pins(task))
                    {
                        // A block of a decision's chain with no object of its own works with the Brain's Target:
                        // no pin, a mark on the node instead (the rule "no hidden variables": shown on the node)
                        if (field.Name == "Target" && field.FieldType == typeof(string) &&
                            string.IsNullOrEmpty(field.GetValue(task) as string) && InDecisionChain(document, state))
                        {
                            var mark = new Label("target: Brain's")
                            {
                                tooltip = "Works with the object the Brain thinks about (its Target pin). Cut the " +
                                          "Brain's Target wire — and this block has nothing to work with"
                            };
                            mark.style.color = new Color(0.95f, 0.75f, 0.2f);
                            mark.style.unityFontStyleAndWeight = FontStyle.Italic;
                            mark.style.paddingLeft = 6;
                            mark.style.paddingTop = 2;
                            extensionContainer.Add(mark);
                            continue;
                        }

                        Port port = Add(Direction.Input, Port.Capacity.Single, ValueFields.PinType(field),
                            ObjectNames.NicifyVariableName(field.Name));
                        port.portColor = TypeColor(port.portType);
                        port.userData = new DataLink { Owner = task, Field = field };
                        if (ValueFields.IsValue(field) && !Attribute.IsDefined(field, typeof(WireOnlyAttribute)))
                            AddPinField(port, task, field, _document);   // a wire-only pin has no number of its own
                        DataPorts.Add(port);
                    }

                    // A node that asks a question — a Branch, a decision (its extra requirement, step 90): the answer
                    // comes by a wire from a condition node into its Condition pin
                    foreach (GraphFields.Link link in ValueFields.ConditionLinks(task))
                    {
                        if (link.IsList)
                            continue;

                        Port asks = Add(Direction.Input, Port.Capacity.Single, typeof(bool),
                            ObjectNames.NicifyVariableName(link.Name));
                        asks.portColor = ConditionColor;
                        asks.userData = new ConditionSlot { Owner = task, Field = link.Name };
                        asks.tooltip = task is ReasonerTask
                            ? "Only if: the decision is taken only while what is wired here holds too (let go on an " +
                              "empty place: the list of conditions)"
                            : "The question: let go on an empty place — the list of conditions";
                        ConditionPorts.Add(asks);
                    }

                    // The values it reads (step 90: a Get, a What, Abilities), each by a wire from a value node
                    ValuePorts.AddRange(AddValuePorts(this, task, inputContainer));
                }

                if (state.HasForegroundTasks && state.Random == null)    // Random leaves by its numbered outputs
                {
                    (string success, string failure, string finished) = OutcomeNames(state);
                    if (finished != null)
                    {
                        // One pin for either outcome: the node is done, the agent goes on
                        _finished = Add(Direction.Output, Port.Capacity.Multi, typeof(Transition), finished);
                        _finished.userData = TransitionTrigger.TasksFinished;
                        _finished.portColor = SuccessColor;
                    }

                    if (success != null)
                    {
                        _success = Add(Direction.Output, Port.Capacity.Multi, typeof(Transition), success);
                        _success.userData = TransitionTrigger.TasksSucceeded;
                        _success.portColor = SuccessColor;
                    }

                    if (failure != null)
                    {
                        _failure = Add(Direction.Output, Port.Capacity.Multi, typeof(Transition), failure);
                        _failure.userData = TransitionTrigger.TasksFailed;
                        _failure.portColor = FailureColor;
                    }
                }

                // A node the flow stays in: its On Tick pin runs a chain of instant nodes once per think
                if (HoldsTheFlow(state))
                {
                    _tick = Add(Direction.Output, Port.Capacity.Multi, typeof(Transition), "On Tick");
                    _tick.userData = TransitionTrigger.Tick;
                    _tick.portColor = new Color(1f, 0.85f, 0.35f);
                    _tick.tooltip = "Once per think while this node runs: the chain wired here runs to its end and the flow " +
                                    "comes back. Only instant nodes: Set, Add…, Set Flag, Branch, Debug Log";
                }


                // A node with nothing to set is small: its title and its pins (Branch, the Brain). The Squad Brain has the
                // formation to set (files, spacing, ranks): it shows its settings
                bool compact = state.Tasks.All(task =>
                    task is BranchTask || task is CombatBrainTask and not SquadBrainTask || task is PickRandomTask);
                bool isMath = state.Tasks.Count == 1 && state.Tasks[0] is NumberTask or SetFlagTask;
                if (isMath)
                {
                    // A node that changes a variable is one row: the variable, the sign, the value
                    var row = new IMGUIContainer(DrawMath);
                    row.style.paddingLeft = row.style.paddingRight = 4;
                    row.style.paddingTop = row.style.paddingBottom = 2;
                    row.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                    extensionContainer.Add(row);
                    compact = true;
                }
                else if (state.Tasks.Count > 0 && !compact)
                {
                    var body = new IMGUIContainer(DrawTasks);
                    body.style.paddingLeft = body.style.paddingRight = 6;
                    body.style.paddingTop = body.style.paddingBottom = 4;
                    body.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                    extensionContainer.Add(body);
                }

                // What the block requires or does by itself, under its settings (step 90: "only with a weapon that
                // shoots", "asks for an attack place while it waits") — every node whose block says something
                foreach (AgentTask task in state.Tasks)
                    AddRequirement(this, task);

                // The Brain: one pin its decisions are wired to (drag it to a decision — the Brain can take it now)
                if (state.IsBrain)
                {
                    Decisions = Add(Direction.Output, Port.Capacity.Multi, typeof(Transition), "Decisions");
                    Decisions.userData = DecisionsPin;
                    Decisions.portColor = DecisionColor;
                    Decisions.tooltip = "Wire a decision here (Fight, Chase, Patrol…): the Brain can take it from now on. " +
                                        "It takes the most important one that holds (1 — the most important)";

                    // Its rules, said on the node itself: nothing about choosing happens that the canvas does not show
                    var rules = new Label(BrainRules)
                    {
                        name = "brain-rules",
                        tooltip = "How the Brain chooses, once per think: the most important decision that holds wins; a " +
                                  "decision just taken is kept a moment (a reaction is taken at once); a blow or an ability " +
                                  "under way is never cut; a " +
                                  "chain that ends hands the choice back; one that did nothing rests a moment"
                    };
                    rules.style.fontSize = 10;
                    rules.style.whiteSpace = WhiteSpace.Normal;
                    rules.style.color = new Color(0.7f, 0.7f, 0.7f);
                    rules.style.paddingLeft = rules.style.paddingRight = 6;
                    rules.style.paddingBottom = 3;
                    extensionContainer.Add(rules);
                }

                // A decision: its priority above the node, "not from" under the title, Do — its chain of blocks, On Tick —
                // a chain of instant blocks once per think while it is in charge (step 90: what Circle keeps asking for)
                ReasonerTask decision = state.Reasoner;
                if (decision != null)
                {
                    BuildDecision(decision);
                    _tick = Add(Direction.Output, Port.Capacity.Multi, typeof(Transition), "On Tick");
                    _tick.userData = TransitionTrigger.Tick;
                    _tick.portColor = new Color(1f, 0.85f, 0.35f);
                    _tick.tooltip = "Once per think while this decision is in charge: the chain wired here runs to its " +
                                    "end (Take Attack Slot, Set, Add…). Only instant blocks";
                }

                // Random: a row per output — its weight, "not twice in a row", its pin — and "+" for one more
                if (state.Random != null)
                    BuildRandom(state.Random);

                // An arrow that is not a block's outcome (a wire that leads on at once — an older graph's) has a row of its
                // own: what it does and its pin
                foreach (Transition transition in state.Transitions)
                {
                    if (state.Random != null && transition.Output >= 0 && transition.Output < state.Random.Outputs.Count)
                        continue;                       // leaves from its numbered output

                    if (OutcomePort(transition.Trigger) != null)
                        continue;

                    if (decision != null && transition.Trigger == TransitionTrigger.Always || state.IsBrain)
                        continue;                       // drawn from the Do / Decisions pin, no row of its own

                    // An arrow from an output Random does not have (a graph built in code): drawn on a row that says so,
                    // to be deleted or wired again — never left without a pin
                    string to = transition.Target != null ? transition.Target.DisplayName : "nowhere";
                    bool lost = state.Random != null && transition.Output >= 0;
                    var row = new Label(lost ? $"output {transition.Output + 1} (Random has no such output) → {to}" : $"at once → {to}")
                    {
                        tooltip = lost
                            ? "Random never picks this output: add outputs, or wire the block from one it has"
                            : "Leaves this node at the next think, before its block is done"
                    };
                    _ownPorts[transition] = AddRow(row, transition);
                }


                // The values the block gives out (step 91: Component and Found of Try Get Component), each an output pin
                foreach (AgentTask task in state.Tasks)
                {
                    foreach (GraphFields.Link link in ValueFields.OutputLinks(task))
                    {
                        if (link.Field.GetValue(task) is not AgentValue given)
                            continue;

                        Port pin = Add(Direction.Output, Port.Capacity.Multi, typeof(AgentValue),
                            ObjectNames.NicifyVariableName(link.Name));
                        pin.portColor = ValueKindColor(given.Kind);
                        pin.userData = new ValueSource { Value = given };
                        pin.tooltip = given.Kind == ValueKind.Flag
                            ? "Gives a flag: wire it into A of a Compare (\"is on\")"
                            : "Gives the component found: wire it into the As pin of a Get (its numbers) or an Is (its flags)";
                        ValueOutPorts.Add(pin);
                    }
                }

                style.minWidth = isMath ? 120f : compact ? 170f : 320f;
                RefreshPorts();
                RefreshExpandedState();
            }

            public AgentState State { get; }

            // The row at the foot of the output pins that folds the free ones into the card and opens them again
            private Label _fold;

            /// <summary>The output pins that lead nowhere are folded into the card now (tests read it).</summary>
            public int FoldedPins { get; private set; }

            /// <summary>
            /// Folds the output pins that lead nowhere into the card, or shows them, as the document keeps it for this node
            /// (a new node starts folded, the project's rule: the card stays small). A pin with a wire always shows — its
            /// wire needs an end. Called once the wires are drawn.
            /// </summary>
            public void ApplyFold()
            {
                bool folded = _document.IsFolded(State);
                int pins = 0;
                FoldedPins = 0;
                foreach (VisualElement child in outputContainer.Children())
                {
                    if (child is not Port port)
                        continue;

                    pins++;
                    bool hide = folded && !port.connected;
                    port.style.display = hide ? DisplayStyle.None : DisplayStyle.Flex;
                    if (hide)
                        FoldedPins++;
                }

                if (pins == 0 || !folded && pins < 2)
                {
                    _fold?.RemoveFromHierarchy();
                    _fold = null;
                    return;
                }

                if (_fold == null)
                {
                    _fold = new Label { tooltip = "Fold the pins that lead nowhere into the card, or show them to wire them" };
                    _fold.style.unityTextAlign = TextAnchor.MiddleRight;
                    _fold.style.color = new Color(0.7f, 0.7f, 0.7f);
                    _fold.style.fontSize = 10;
                    _fold.style.paddingRight = 8;
                    _fold.style.paddingTop = 2;
                    _fold.style.paddingBottom = 2;
                    _fold.RegisterCallback<MouseDownEvent>(evt =>
                    {
                        _document.SetFolded(State, !_document.IsFolded(State));
                        ApplyFold();
                        RefreshPorts();
                        evt.StopPropagation();
                    });
                }

                outputContainer.Add(_fold);             // always the last row
                _fold.text = folded ? (FoldedPins > 0 ? $"▸ {FoldedPins} more" : "") : "▾ fold";
                _fold.style.display = folded && FoldedPins == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            }

            public List<Port> DataPorts { get; } = new();

            /// <summary>The pins a Branch, a decision takes its answer through (empty — the node asks nothing).</summary>
            public List<Port> ConditionPorts { get; } = new();

            /// <summary>The pins the node takes value nodes through (a Get, a What, Abilities).</summary>
            public List<Port> ValuePorts { get; } = new();

            /// <summary>The pins the block gives values out through (step 91: Component, Found).</summary>
            public List<Port> ValueOutPorts { get; } = new();

            /// <summary>What a condition added to this node answers to: its block that asks (a Branch, a decision); null — none.</summary>
            public object ConditionAsker() => ConditionPorts.Count > 0 ? ((ConditionSlot)ConditionPorts[0].userData).Owner : null;

            public Port In { get; }
            public Port Out { get; private set; }

            /// <summary>
            /// A mark at the input: an arrow drawn as a link comes in here from that node (the other end of the link).
            /// A click shows the node the arrow comes from, a double click goes to it.
            /// </summary>
            public void AddIncoming(string text, Action point, Action open)
            {
                var mark = new Label(text)
                {
                    tooltip = "An arrow drawn as a link comes from this node. Click: show it; double click: go to it"
                };
                mark.style.backgroundColor = LinkColor;
                mark.style.color = Color.white;
                mark.style.fontSize = 11;
                mark.style.alignSelf = Align.FlexStart;
                mark.style.marginLeft = 4;
                mark.style.marginTop = mark.style.marginBottom = 2;
                mark.style.paddingLeft = mark.style.paddingRight = 6;
                mark.style.paddingTop = mark.style.paddingBottom = 1;
                mark.style.borderTopLeftRadius = mark.style.borderTopRightRadius =
                    mark.style.borderBottomLeftRadius = mark.style.borderBottomRightRadius = 3f;
                mark.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    evt.StopPropagation();
                    if (evt.clickCount == 2)
                        schedule.Execute(open);
                    else
                        point();
                });
                inputContainer.Add(mark);
            }

            /// <summary>The output the edge leaves from: a task outcome pin, or the pin of its own row.</summary>
            public Port PortFor(Transition transition)
            {
                if (transition.Output >= 0 && _outputPorts.TryGetValue(transition.Output, out Port numbered))
                    return numbered;
                if (Decisions != null)
                    return Decisions;                   // every wire of the Brain leaves its Decisions pin (a wire to a
                                                        // block is the graph's problem, still drawn and deletable)
                if (_do != null && transition.Trigger == TransitionTrigger.Always)
                    return _do;

                return OutcomePort(transition.Trigger) ?? (_ownPorts.TryGetValue(transition, out Port own) ? own : Out);
            }

            /// <summary>The node is reached from a decision's Do pin (directly or along the chain of blocks after it).</summary>
            internal static bool InDecisionChain(GraphDocument document, AgentState state)
            {
                List<AgentState> states = document.States();
                var seen = new HashSet<AgentState>();
                var open = new Stack<AgentState>();
                open.Push(state);
                while (open.Count > 0)
                {
                    AgentState at = open.Pop();
                    if (!seen.Add(at))
                        continue;

                    foreach (AgentState other in states)
                    {
                        if (!other.Transitions.Exists(arrow => arrow.Target == at) || other.IsBrain)
                            continue;

                        if (other.Reasoner != null)
                            return true;

                        open.Push(other);
                    }
                }

                return false;
            }

            /// <summary>The mark of the Brain's pin the decisions are wired to.</summary>
            public static readonly object DecisionsPin = new();

            private static readonly Color DecisionColor = new(0.95f, 0.75f, 0.2f);

            /// <summary>The Brain's pin its decisions are wired to (null — not a Brain).</summary>
            public Port Decisions { get; }

            /// <summary>The Brain's rules as its node says them (BehaviorGraphInstance's numbers).</summary>
            public static string BrainRules =>
                $"keeps a decision ≥ {BehaviorGraphInstance.MIN_DECISION_TIME:0.#} s (a reaction: at once) · never cuts a blow · " +
                $"a chain that did nothing rests {BehaviorGraphInstance.EMPTY_CHAIN_REST:0.#} s";

            private Port _do;
            private Label _priority;
            private Label _verdict;
            private Label _noDecision;

            // A decision on the canvas: a square with its priority above the node, a row "not from: …", a line that
            // says in Play why it is not taken, and the pin Do its chain of ready blocks starts from
            private void BuildDecision(ReasonerTask decision)
            {
                _priority = new Label(decision.Priority.ToString())
                {
                    tooltip = "Priority: 1 — the most important. When several decisions hold, the Brain takes the most " +
                              "important one. Click to change"
                };
                _priority.style.position = Position.Absolute;
                _priority.style.top = -30f;
                _priority.style.left = 8f;
                _priority.style.width = 28f;
                _priority.style.height = 26f;
                _priority.style.unityTextAlign = TextAnchor.MiddleCenter;
                _priority.style.unityFontStyleAndWeight = FontStyle.Bold;
                _priority.style.fontSize = 15;
                _priority.style.color = Color.black;
                _priority.style.borderTopLeftRadius = _priority.style.borderTopRightRadius =
                    _priority.style.borderBottomLeftRadius = _priority.style.borderBottomRightRadius = 3f;
                _priority.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    evt.StopPropagation();
                    var menu = new GenericMenu();
                    for (int number = 1; number <= 12; number++)
                    {
                        int picked = number;
                        menu.AddItem(new GUIContent(picked.ToString()), decision.Priority == picked, () =>
                        {
                            decision.Priority = picked;
                            _document.Touch();
                        });
                    }

                    menu.ShowAsContext();
                });
                hierarchy.Add(_priority);

                PaintPriority(decision);

                _verdict = new Label();
                _verdict.style.color = new Color(0.95f, 0.42f, 0.42f);
                _verdict.style.paddingLeft = 6;
                _verdict.style.whiteSpace = WhiteSpace.Normal;
                _verdict.style.display = DisplayStyle.None;
                extensionContainer.Add(_verdict);

                var notFrom = new IMGUIContainer(() => DrawNotFrom(decision));
                notFrom.style.paddingLeft = notFrom.style.paddingRight = 6;
                notFrom.style.paddingTop = notFrom.style.paddingBottom = 2;
                notFrom.style.backgroundColor = new Color(0.22f, 0.16f, 0.16f, 0.9f);
                extensionContainer.Add(notFrom);

                _do = Add(Direction.Output, Port.Capacity.Single, typeof(Transition), "Do");
                _do.userData = TransitionTrigger.Always;
                _do.portColor = SuccessColor;
                _do.tooltip = "The ready blocks the agent does once this decision is taken, one after another. When " +
                              "the chain ends, the Brain chooses again";
                Out = _do;
            }

            // Random on the canvas: a row per output — its number, its weight, "not twice", × — with its pin on the right,
            // and a button for one more output under them
            private void BuildRandom(PickRandomTask random)
            {
                for (int i = 0; i < random.Outputs.Count; i++)
                {
                    int number = i;
                    var row = new IMGUIContainer(() => DrawRandomOutput(random, number));
                    Port pin = AddRow(row, new RandomPin { Index = number });
                    pin.portColor = SuccessColor;
                    pin.tooltip = "Wire a block here: when this output is picked, the flow goes down it. Wire the " +
                                  "block's end back into Random — and it rolls again";
                    _outputPorts[number] = pin;
                }

                var add = new Button(() => _document.AddRandomOutput(random)) { text = "+ output" };
                add.tooltip = "One more output: a block of its own, picked by its weight";
                add.style.alignSelf = Align.FlexStart;
                add.style.marginLeft = 6;
                add.style.marginTop = add.style.marginBottom = 3;
                extensionContainer.Add(add);
            }

            // "1  weight [3]  ☐ not twice  ×"
            private void DrawRandomOutput(PickRandomTask random, int number)
            {
                if (number >= random.Outputs.Count)
                    return;

                RandomOutput output = random.Outputs[number];
                float labelWidth = EditorGUIUtility.labelWidth;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    GUILayout.Label((number + 1).ToString(), Strong, GUILayout.Width(14f));
                    EditorGUIUtility.labelWidth = 44f;
                    float weight = EditorGUILayout.FloatField(new GUIContent("weight", "How often, against the other " +
                        "outputs' weights. 0 — never"), output.Weight, GUILayout.Width(90f));
                    bool notTwice = GUILayout.Toggle(output.NotTwiceInARow, new GUIContent("not twice",
                        "Never picked right after itself (while another output can be picked)"), GUILayout.ExpandWidth(false));
                    if (EditorGUI.EndChangeCheck())
                    {
                        output.Weight = Mathf.Max(0f, weight);
                        output.NotTwiceInARow = notTwice;
                        _document.TouchQuietly();
                    }

                    GUILayout.FlexibleSpace();
                    if (random.Outputs.Count > 1 && GUILayout.Button(new GUIContent("×", "Take this output off, with its wire"),
                            EditorStyles.miniButton, GUILayout.Width(18f)))
                    {
                        AgentState state = State;
                        EditorApplication.delayCall += () => _document.RemoveRandomOutput(state, number);
                    }
                }

                EditorGUIUtility.labelWidth = labelWidth;
            }

            // Yellow — the priority is the decision's own; red — another decision of the same Brain has it too
            private void PaintPriority(ReasonerTask decision)
            {
                bool shared = DecisionsAlongside().Any(other => other.Reasoner.Priority == decision.Priority);
                _priority.style.backgroundColor = shared ? new Color(0.9f, 0.3f, 0.3f) : DecisionColor;
                _priority.tooltip = shared
                    ? $"Priority {decision.Priority} — another decision of this Brain has it too: between them the one " +
                      "the agent acts on keeps, else the one made earlier. Give them different numbers"
                    : "Priority: 1 — the most important. When several decisions hold, the Brain takes the most important " +
                      "one. Click to change";
            }

            // The Brains this decision is wired to
            private IEnumerable<AgentState> BrainsOfThis() =>
                _document.States().Where(brain => brain.IsBrain && brain.Transitions.Any(wire => wire.Target == State));

            // The other decisions of the Brains this one is wired to
            private IEnumerable<AgentState> DecisionsAlongside() =>
                BrainsOfThis().SelectMany(brain => brain.Transitions)
                    .Select(wire => wire.Target)
                    .Where(other => other != null && other != State && other.Reasoner != null)
                    .Distinct();

            // "not from: Fight ×  Swim ×  +" — the decisions this one cannot be taken from
            private void DrawNotFrom(ReasonerTask decision)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(decision.NotFrom.Count == 0 ? "from any decision" : "not from:", Muted,
                        GUILayout.ExpandWidth(false));
                    foreach (int id in decision.NotFrom.ToList())
                    {
                        AgentState other = _document.States().Find(found => found.Id == id);
                        GUILayout.Label(other != null ? other.DisplayName : "?", Strong, GUILayout.ExpandWidth(false));
                        if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(18f)))
                        {
                            int removed = id;
                            EditorApplication.delayCall += () =>
                            {
                                decision.NotFrom.Remove(removed);
                                _document.Touch();
                            };
                        }
                    }

                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("+ not from", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                    {
                        var menu = new GenericMenu();
                        foreach (AgentState other in DecisionsAlongside())
                        {
                            if (decision.NotFrom.Contains(other.Id))
                                continue;

                            AgentState picked = other;
                            menu.AddItem(new GUIContent(other.DisplayName), false, () =>
                            {
                                decision.NotFrom.Add(picked.Id);
                                _document.Touch();
                            });
                        }

                        if (menu.GetItemCount() == 0)
                            menu.AddDisabledItem(new GUIContent("No other decision is wired to the Brain"));
                        menu.ShowAsContext();
                    }
                }
            }

            // In Play: the decision the Brain took is lit; the others say why not ("target 6.2 m, needs 1.9 or
            // closer", "not from Fight") or that a more important one won; the Brain with none says so
            private void RefreshDecision()
            {
                AgentContext live = LiveContext();
                if (_verdict != null)
                {
                    // The agent runs its own copy of the graph (compiled from the asset): its states are found by id
                    ReasonerVerdict? mine = null;
                    if (live != null)
                    {
                        foreach (AgentState brain in BrainsOfThis())
                        {
                            AgentState running = Running(brain);
                            if (running == null)
                                continue;

                            foreach (ReasonerVerdict verdict in CombatBrainTask.VerdictsOf(running, live))
                            {
                                if (verdict.State != null && verdict.State.Id == State.Id)
                                    mine = verdict;
                            }
                        }
                    }

                    string text = mine is not { } seen ? null
                        : seen.Won ? null
                        : seen.Holds ? seen.WhyNot ?? "holds — a more important decision won"
                        : $"✗ {seen.WhyNot ?? "does not hold"}";
                    _verdict.text = text ?? "";
                    _verdict.style.display = text != null ? DisplayStyle.Flex : DisplayStyle.None;
                    _verdict.style.color = mine is { Holds: true } ? new Color(0.72f, 0.72f, 0.72f) : new Color(0.95f, 0.42f, 0.42f);
                    if (_priority != null)
                        _priority.style.borderBottomWidth = mine is { Won: true } ? 3f : 0f;
                    if (mine is { Won: true })
                        SetBorder(WonBorder, 3f);
                    else if (!_isActive)
                        SetBorder(Color.clear, 0f);
                }

                if (Decisions == null)
                    return;

                AgentState runningBrain = live != null ? Running(State) : null;
                IReadOnlyList<ReasonerVerdict> all = runningBrain != null ? CombatBrainTask.VerdictsOf(runningBrain, live) : null;
                bool none = all != null && all.Count > 0 && all.All(verdict => !verdict.Won);
                if (_noDecision == null)
                {
                    _noDecision = new Label("no decision: none holds — the agent stands");
                    _noDecision.style.color = new Color(0.95f, 0.42f, 0.42f);
                    _noDecision.style.paddingLeft = 6;
                    extensionContainer.Add(_noDecision);
                    RefreshExpandedState();
                }

                _noDecision.style.display = none ? DisplayStyle.Flex : DisplayStyle.None;
            }

            private static readonly Color WonBorder = new(0.35f, 0.85f, 0.35f);

            // The state of the graph the followed agent runs that is this node of the canvas (the same id); null — none
            private static AgentState Running(AgentState drawn)
            {
                BehaviorGraph running = ElementInspector.Agent != null ? ElementInspector.Agent.Graph?.Graph : null;
                if (running == null || drawn == null)
                    return null;

                RunningStates.Clear();
                running.CollectStates(RunningStates);
                return RunningStates.Find(state => state.Id == drawn.Id);
            }

            private static readonly List<AgentState> RunningStates = new();

            private Port OutcomePort(TransitionTrigger trigger) => trigger switch
            {
                TransitionTrigger.TasksSucceeded => _success,
                TransitionTrigger.TasksFailed => _failure,
                TransitionTrigger.TasksFinished => _finished,
                TransitionTrigger.Tick => _tick,
                _ => null
            };

            // A row at the bottom of the node: what it says on the left, an output pin on the right
            private Port AddRow(VisualElement content, object portData, Port lead = null)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.9f);
                row.style.borderTopWidth = 1f;
                row.style.borderTopColor = new Color(0.1f, 0.1f, 0.1f);
                content.style.flexGrow = 1;
                content.style.paddingLeft = 6;
                content.style.paddingTop = content.style.paddingBottom = 3;
                Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi,
                    typeof(Transition));
                port.portName = "";
                port.userData = portData;
                port.portColor = ConditionColor;        // an arrow by a condition
                if (lead != null)
                    row.Add(lead);
                row.Add(content);
                row.Add(port);
                extensionContainer.Add(row);
                return port;
            }

            // A node the flow stays in for a while: a task that lasts over time — it repeats (Strike), never ends
            // (Patrol Area) or ends by itself later (Circle Target, Chase, Wait). Not an instant node (Set, Branch) and
            // not a mark of flow (the Brain, a decision — the flow passes through them)
            internal static bool HoldsTheFlow(AgentState state)
            {
                foreach (AgentTask task in state.Tasks)
                {
                    if (task.IsBackground || task.IsInstant || task.IsOneShot ||
                        task is CombatBrainTask or BranchTask or ReasonerTask)
                        continue;

                    return true;
                }

                return false;
            }

            // The colour of the title by what the node is (NodeKinds): the Brain, a decision, or a block by its group —
            // Branch is red (the user's word, step 37)
            private static Color? TitleColor(AgentState state)
            {
                foreach (AgentTask task in state.Tasks)
                {
                    if (!task.IsBackground)
                        return NodeKinds.TitleColor(task.GetType());
                }

                return null;
            }

            private static GUIStyle _muted;

            // The words that only hold the sentence together ("when", "and", "→ Circle Target")
            private static GUIStyle Muted => _muted ??= new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(0.72f, 0.72f, 0.72f) }
            };

            // The words that say what must happen
            private static GUIStyle Strong => EditorStyles.boldLabel;

            /// <summary>The Brain and its decisions show the agent as it is now: the decision taken, why the others not.</summary>
            public void RefreshLive()
            {
                if (Decisions != null || State.Reasoner != null)
                    RefreshDecision();
            }

            // The agent the window follows, while the game runs (null — nothing live to show)
            private static AgentContext LiveContext() =>
                Application.isPlaying && ElementInspector.Agent != null ? ElementInspector.Agent.Context : null;

            public void SetActive(Color color, float timeInState)
            {
                _isActive = true;
                SetBorder(color, 3f);
                int tenths = timeInState < 0f ? -1 : Mathf.FloorToInt(timeInState * 10f);
                if (tenths == _shownTenths)
                    return;

                _shownTenths = tenths;
                _status.text = tenths < 0 ? "" : $"▶ {tenths / 10f:0.0} s";
            }

            public void SetIdle()
            {
                if (!_isActive)
                    return;

                _isActive = false;
                _shownTenths = int.MinValue;
                _status.text = "";
                SetBorder(Color.clear, 0f);
            }

            private void SetBorder(Color color, float width)
            {
                style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = color;
                style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = width;
                style.borderTopLeftRadius = style.borderTopRightRadius =
                    style.borderBottomLeftRadius = style.borderBottomRightRadius = 8f;
            }

            // The tasks' own fields, edited in place; the name and the flags of a task are in the side panel
            private static string SignOf(AgentTask task) => task switch
            {
                AddNumberTask => "+",
                SubtractNumberTask => "\u2212",
                MultiplyNumberTask => "\u00d7",
                DivideNumberTask => "\u00f7",
                _ => "="
            };

            // "Attacks + 1": the variable, the sign of the node, the value — a number, or another variable (B)
            private void DrawMath()
            {
                AgentTask task = State.Tasks[0];
                EditorGUI.BeginChangeCheck();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (task is SetFlagTask flag)
                    {
                        Wired(flag.Variable);
                        GUILayout.Label("=", GUILayout.Width(12f));
                        flag.Value = EditorGUILayout.Toggle(flag.Value, GUILayout.Width(16f));
                    }
                    else if (task is NumberTask number)
                    {
                        Wired(number.Variable);
                        GUILayout.Label(SignOf(task), GUILayout.Width(12f));

                        object value = number.Value;
                        if (ValueFields.Uses(value))
                            Wired(ValueFields.NameOf(value));       // a number with no wire is typed in the Value pin
                    }
                }

                if (EditorGUI.EndChangeCheck())
                    _document.TouchQuietly();
            }

            // The name of the variable that came into a pin by a wire ("variable?" — nothing is wired in yet)
            private static void Wired(string variable)
            {
                bool none = string.IsNullOrEmpty(variable);
                var name = new GUIContent(none ? "variable?" : variable,
                    none ? "Drag a variable from the Blackboard and wire it into the pin" : "");
                GUILayout.Label(name, none ? EditorStyles.centeredGreyMiniLabel : EditorStyles.label,
                    GUILayout.Width(Mathf.Clamp(EditorStyles.label.CalcSize(name).x + 4f, 40f, 150f)));
            }

            // The block's own settings, edited in place: no name over them (the title says what the node is), labels as
            // wide as the longest of them (step 90: "Needs Attack Place", not "Needs Attack Plac")
            private void DrawTasks()
            {
                float labelWidth = EditorGUIUtility.labelWidth;
                ElementInspector.Compact = true;
                ElementInspector.Wired = true;              // an object comes by a wire into the node's data pin
                ElementInspector.ValuesInPins = true;       // numbers and flags are typed in their pins
                float widest = 0f;
                foreach (AgentTask task in State.Tasks)
                    widest = Mathf.Max(widest, ElementInspector.LabelWidthFor(task, ownOnly: true));
                EditorGUIUtility.labelWidth = widest;
                EditorGUI.BeginChangeCheck();
                foreach (AgentTask task in State.Tasks)
                    ElementInspector.Fields(_document, task, ownOnly: true);

                ElementInspector.Wired = false;
                ElementInspector.ValuesInPins = false;
                ElementInspector.Compact = false;

                if (EditorGUI.EndChangeCheck())
                    _document.TouchQuietly();
                EditorGUIUtility.labelWidth = labelWidth;
            }

            // One foreground task names the outputs after itself; several — the state's outputs are generic
            internal static (string Success, string Failure, string Finished) OutcomeNames(AgentState state)
            {
                AgentTask only = null;
                foreach (AgentTask task in state.Tasks)
                {
                    if (task.IsBackground)
                        continue;

                    if (only != null)
                        return ("Success", "Failure", null);

                    only = task;
                }

                var info = (AIKitNodeAttribute)Attribute.GetCustomAttribute(only.GetType(), typeof(AIKitNodeAttribute));
                if (info != null && info.Endless)
                    return (null, null, null);          // never finishes: nothing to draw an outcome arrow from

                if (!string.IsNullOrEmpty(info?.Finished))
                    return (null, null, info.Finished);     // one outcome: used or not, the node is done

                string failure = info != null && info.NeverFails
                    ? null
                    : string.IsNullOrEmpty(info?.Failure) ? "Failure" : info.Failure;

                // A task that repeats (an attack) has no "done": the node is left by its failure pin only
                // …unless it names one itself: the pin then fires after each step (Attack: "Done" after each blow)
                string success = only.Repeats
                    ? string.IsNullOrEmpty(info?.Success) ? null : info.Success
                    : string.IsNullOrEmpty(info?.Success) ? "Success" : info.Success;
                return (success, failure, null);
            }

            private Port Add(Direction direction, Port.Capacity capacity, Type type, string name)
            {
                Port port = InstantiatePort(Orientation.Horizontal, direction, capacity, type);
                port.portName = name;
                (direction == Direction.Input ? inputContainer : outputContainer).Add(port);
                return port;
            }
        }
    }
}
