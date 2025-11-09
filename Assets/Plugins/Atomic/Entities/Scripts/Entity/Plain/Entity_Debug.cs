#if UNITY_EDITOR && ODIN_INSPECTOR
using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;

namespace Atomic.Entities
{
    public partial class Entity
    {
        [ShowInInspector]
        [LabelText("Name")]
        private string NameDebug
        {
            get => this.Name;
            set => this.Name = value;
        }

        [LabelText("Initialized")]
        [ShowInInspector]
        private bool InitializedDebug => Initialized;

        [ShowInInspector]
        [LabelText("Enabled")]
        private bool EnabledDebug => Enabled;

        ///Tags
        private static readonly List<TagElement> _tagElememtsCache = new();

        [InlineProperty]
        private struct TagElement : IComparable<TagElement>
        {
            [ShowInInspector, ReadOnly]
            internal string name;
            internal readonly int id;

            public TagElement(string name, int id)
            {
                this.name = name;
                this.id = id;
            }

            public int CompareTo(TagElement other)
            {
                return string.Compare(this.name, other.name, StringComparison.Ordinal);
            }
        }

        [Searchable]
        [LabelText("Tags")]
        [ShowInInspector, PropertyOrder(100)]
        [ListDrawerSettings(
            CustomRemoveElementFunction = nameof(RemoveTagElement),
            CustomRemoveIndexFunction = nameof(RemoveTagElementAt),
            HideAddButton = true
        )]
        private List<TagElement> TagElememts
        {
            get
            {
                _tagElememtsCache.Clear();

                IReadOnlyCollection<int> tags = Tags;
                if (tags == null)
                    return _tagElememtsCache;

                foreach (int tag in tags)
                {
                    string name = EntityUtils.IdToName(tag);
                    _tagElememtsCache.Add(new TagElement(name, tag));
                }

                _tagElememtsCache.Sort();
                return _tagElememtsCache;
            }
            set
            {
                /** noting... **/
            }
        }

        private void RemoveTagElement(TagElement tagElement)
        {
            this.DelTag(tagElement.id);
        }

        private void RemoveTagElementAt(int index)
        {
           this.DelTag(this.TagElememts[index].id);
        }

        ///Values
        [InlineProperty]
        private struct ValueElement : IComparable<ValueElement>
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

            public int CompareTo(ValueElement other)
            {
                return string.Compare(this.name, other.name, StringComparison.Ordinal);
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
        private List<ValueElement> ValueElements
        {
            get
            {
                List<ValueElement> result = new List<ValueElement>();
                IReadOnlyDictionary<int, object> values = this.Values;
                if (values == null)
                    return result;

                foreach ((int id, object value) in values)
                {
                    string name = EntityUtils.IdToName(id);
                    result.Add(new ValueElement(name, value, id));
                }

                result.Sort();
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
            this.DelValue(this.ValueElements[index].id);
        }

        ///Behaviours
        private static readonly List<BehaviourElement> _behaviourElementsCache = new();

        [InlineProperty]
        private struct BehaviourElement : IComparable<BehaviourElement>
        {
            [ShowInInspector, ReadOnly]
            public string name;

            internal readonly IEntityBehaviour value;

            public BehaviourElement(string name, IEntityBehaviour value)
            {
                this.name = name;
                this.value = value;
            }

            public int CompareTo(BehaviourElement other)
            {
                return string.Compare(this.name, other.name, StringComparison.Ordinal);
            }
        }

        [Searchable]
        [LabelText("Behaviours")]
        [ShowInInspector, PropertyOrder(100)]
        [ListDrawerSettings(
            CustomRemoveElementFunction = nameof(RemoveLogicElement),
            CustomRemoveIndexFunction = nameof(RemoveLogicElementAt),
            HideAddButton = true
        )]
        private List<BehaviourElement> BehaviourElements
        {
            get
            {
                _behaviourElementsCache.Clear();

                IReadOnlyCollection<IEntityBehaviour> behaviours = Behaviours;
                if (behaviours == null)
                    return _behaviourElementsCache;

                foreach (IEntityBehaviour behaviour in behaviours)
                {
                    string name = behaviour.GetType().Name;
                    _behaviourElementsCache.Add(new BehaviourElement(name, behaviour));
                }

                _behaviourElementsCache.Sort();
                return _behaviourElementsCache;
            }
            set
            {
                /** noting... **/
            }
        }

        private void RemoveLogicElement(BehaviourElement behaviourElement)
        {
            this.DelBehaviour(behaviourElement.value);
        }

        private void RemoveLogicElementAt(int index)
        {
            this.DelBehaviour(this.BehaviourElements[index].value);
        }
    }
}
#endif