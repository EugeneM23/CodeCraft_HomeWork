#if UNITY_EDITOR && ODIN_INSPECTOR
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Atomic.Contexts
{
    public partial class Context
    {
        ///Main
        [PropertySpace(12)]
        [ShowInInspector, ReadOnly]
        [HideInEditorMode, LabelText("Name")]
        private string NameDebug
        {
            get { return this.Name; }
            set { this.Name = value; }
        }

        [ShowInInspector, ReadOnly, PropertySpace]
        [HideInEditorMode, LabelText("Initialized")]
        private bool InitializedDebug => this.Initialized;

        [ShowInInspector, ReadOnly]
        [HideInEditorMode, LabelText("Enabled")]
        private bool EnabledDebug => this.Enabled;

        ///Values
        private struct ValueElement
        {
            [HorizontalGroup(200), ShowInInspector, ReadOnly, HideLabel]
            public string name;

            [HorizontalGroup, ShowInInspector, HideLabel]
            public object value;

            internal readonly int id;

            public ValueElement(string name, object value, int id)
            {
                this.name = name;
                this.value = value;
                this.id = id;
            }
        }

        [Searchable]
        [LabelText("Values")]
        [ShowInInspector, PropertyOrder(100)]
        [ListDrawerSettings(
            CustomRemoveElementFunction = nameof(RemoveValueElement),
            CustomRemoveIndexFunction = nameof(RemoveValueElementAt),
            HideAddButton = true
        )]
        private List<ValueElement> ValuesDebug
        {
            get
            {
                var result = new List<ValueElement>();

                IReadOnlyDictionary<int, object> values = this.Values;
                if (values == null)
                    return result;

                foreach ((int id, object value) in values)
                {
                    string name = ContextUtils.IdToName(id);
                    result.Add(new ValueElement(name, value, id));
                }

                return result;
            }

            set
            {
                /** noting... **/
            }
        }

        private void RemoveValueElement(ValueElement valueElement)
        {
            this.DelValue(valueElement.id);
        }

        private void RemoveValueElementAt(int index)
        {
            this.DelValue(this.ValuesDebug[index].id);
        }

        ///Logics
        private static readonly List<ControllerElement> _controllerElementsCache = new();

        [InlineProperty]
        private struct ControllerElement
        {
            [ShowInInspector, ReadOnly, HideLabel]
            public string name;

            internal readonly IContextController value;

            public ControllerElement(IContextController value)
            {
                this.name = value.GetType().Name;
                this.value = value;
            }
        }

        [Searchable]
        [LabelText("Controllers")]
        [ShowInInspector, PropertyOrder(100)]
        [ListDrawerSettings(
            CustomRemoveElementFunction = nameof(RemoveControllerDebug),
            CustomRemoveIndexFunction = nameof(RemoveControllerDebugAt)
        )]
        private List<ControllerElement> ControllersDebug
        {
            get
            {
                _controllerElementsCache.Clear();

                IReadOnlyCollection<IContextController> controllers = this.Controllers;
                if (controllers == null)
                    return _controllerElementsCache;

                foreach (var controller in controllers)
                    _controllerElementsCache.Add(new ControllerElement(controller));

                return _controllerElementsCache;
            }
            set
            {
                /** noting... **/
            }
        }

        [ShowInInspector, PropertyOrder(100)]
        [Button("Add Controller"), HideInEditorMode]
        private void AddControllerDebug(IContextController controller) => this.AddController(controller);

        [ShowInInspector, PropertyOrder(100)]
        [Button("Add Value"), HideInEditorMode]
        private void AddValueDebug(int key, object value) => this.AddValue(key, value);

        private void RemoveControllerDebug(ControllerElement controllerElement)
        {
            this.DelController(controllerElement.value);
        }

        private void RemoveControllerDebugAt(int index)
        {
            this.DelController(this.ControllersDebug[index].value);
        }
    }
}
#endif