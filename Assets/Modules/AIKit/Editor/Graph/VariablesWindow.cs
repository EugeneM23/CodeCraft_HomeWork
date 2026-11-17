using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace AIKit.Editor
{
    /// <summary>
    /// The variables of the behavior in a window of their own (15.11.2026, the user's word: "the variables we made — not on
    /// the canvas, in another window"; Unreal's My Blueprint panel). It follows the graph the Behavior Graph window shows:
    /// one row per variable — its colour, name, kind and value (the start value while editing, the agent's own in Play);
    /// "+" adds one, a double click on the name renames it, Delete deletes it, and a row dragged onto the canvas becomes a
    /// node of that variable there (or is wired into the node it is dropped on). The rows are the Blackboard panel's
    /// (BlackboardPanel, Part.Variables), held by a GraphView of the window's own so that they are picked, renamed and
    /// dragged as on the canvas. The canvas keeps the character's own numbers and states (Part.Character).
    /// </summary>
    public sealed class VariablesWindow : EditorWindow
    {
        private Host _host;
        private BlackboardPanel _panel;
        private GraphDocument _document;

        private static GraphDocument _followed;

        [MenuItem("Window/AI Kit/Variables")]
        public static void Open()
        {
            var window = GetWindow<VariablesWindow>("Variables");
            window.Show(_followed);
        }

        /// <summary>The graph the Behavior Graph window shows now: every open Variables window lists its variables.</summary>
        public static void Follow(GraphDocument document)
        {
            _followed = document;
            foreach (VariablesWindow window in Resources.FindObjectsOfTypeAll<VariablesWindow>())
                window.Show(document);
        }

        /// <summary>The panel of variables the window holds (tests read it).</summary>
        public BlackboardPanel Panel => _panel;

        private void OnEnable()
        {
            rootVisualElement.Clear();
            _host = new Host(this);
            _host.style.flexGrow = 1;
            _panel = new BlackboardPanel(_host, BlackboardPanel.Part.Variables) { windowed = true };
            _host.Add(_panel);
            rootVisualElement.Add(_host);
            Show(_followed);
        }

        /// <summary>Lists the variables of the graph (null — nothing to list).</summary>
        public void Show(GraphDocument document)
        {
            _document = document;
            _panel?.Refresh(document);
        }

        /// <summary>
        /// A GraphView of the window's own: it only holds the panel, so that its rows are picked, renamed, deleted and
        /// dragged out as rows of a blackboard are (a picked row deleted — the variable goes, with every wire of it).
        /// </summary>
        private sealed class Host : GraphView
        {
            private readonly VariablesWindow _window;

            public Host(VariablesWindow window)
            {
                _window = window;
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new ClickSelector());
                graphViewChanged = change =>
                {
                    var deleted = change.elementsToRemove?.OfType<BlackboardField>()
                        .Select(BlackboardPanel.VariableOf).Where(name => name != null).ToList();
                    change.elementsToRemove?.RemoveAll(element => element is BlackboardField);
                    if (deleted != null && deleted.Count > 0 && _window._document != null)
                        schedule.Execute(() => deleted.ForEach(name => _window._document.DeleteVariable(name)));
                    return change;
                };
            }
        }
    }
}
