using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>
    /// The fields of a node that can take their value from the blackboard: a name of an object variable, and a
    /// "number or variable" field (BlackboardValue) — how the editor reads them, binds them to a variable and lets
    /// them go. A field bound to a variable is a data pin of its node on the canvas: the wire into it comes from a
    /// node of that variable (handbook §8: the blackboard is the one channel of data between nodes; §32.2: "a
    /// constant or a variable" by a switch).
    /// </summary>
    public static class ValueFields
    {
        private const BindingFlags HIDDEN = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>The field is a "number or variable" one.</summary>
        public static bool IsValue(FieldInfo field) =>
            field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(BlackboardValue<>);

        /// <summary>The kind of variable a "number or variable" field can be bound to; null — none the canvas knows.</summary>
        public static VariableType? KindOf(Type valueType)
        {
            Type inner = valueType.IsGenericType ? valueType.GetGenericArguments()[0] : valueType;
            if (inner == typeof(float))
                return VariableType.Float;
            if (inner == typeof(int))
                return VariableType.Int;
            if (inner == typeof(bool))
                return VariableType.Bool;
            return null;
        }

        public static bool Uses(object boxed) => (bool)boxed.GetType().GetField("_useVariable", HIDDEN).GetValue(boxed);

        public static string NameOf(object boxed) => (string)boxed.GetType().GetField("_name", HIDDEN).GetValue(boxed);

        public static object Constant(object boxed) => boxed.GetType().GetField("_constant", HIDDEN).GetValue(boxed);

        /// <summary>The value now comes from the variable (the constant stays as what is used with no blackboard).</summary>
        public static object ToVariable(object boxed, string name)
        {
            Type type = boxed.GetType();
            type.GetField("_useVariable", HIDDEN).SetValue(boxed, true);
            type.GetField("_name", HIDDEN).SetValue(boxed, name);
            type.GetField("_scope", HIDDEN).SetValue(boxed, BlackboardScope.Agent);
            return boxed;
        }

        /// <summary>The value is a plain number of the node again (value — the number to keep; null — the one it had).</summary>
        public static object ToConstant(object boxed, object value = null)
        {
            Type type = boxed.GetType();
            type.GetField("_useVariable", HIDDEN).SetValue(boxed, false);
            type.GetField("_name", HIDDEN).SetValue(boxed, "");
            FieldInfo constant = type.GetField("_constant", HIDDEN);
            if (value != null && constant.FieldType.IsInstanceOfType(value))
                constant.SetValue(boxed, value);

            return boxed;
        }

        /// <summary>
        /// The fields of an element that are data pins on the canvas: every object-variable name, and every "number or
        /// variable" field that is bound to a variable now.
        /// </summary>
        public static IEnumerable<FieldInfo> Pins(object element)
        {
            foreach (FieldInfo field in element.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (element is IReadsMembers reads && !reads.Shows(field.Name))
                    continue;                           // a Get / Is after a Cast: its own object pin is not used

                if (field.FieldType == typeof(string) &&
                    field.GetCustomAttribute<VariableNameAttribute>() is { HasType: true, Type: VariableType.Transform })
                    yield return field;
                else if (field.FieldType == typeof(string) && field.Name == "Variable" && element is NumberTask or SetFlagTask)
                    yield return field;                 // the variable a Set / Add node changes comes by a wire
                else if (field.FieldType == typeof(string) && element is DebugLogTask && field.Name == nameof(DebugLogTask.Value))
                    yield return field;                 // Debug Log writes a variable of any kind: it comes by a wire
                else if (IsValue(field) && KindOf(field.FieldType) != null)
                    yield return field;                 // a number or a flag: typed in the pin, or wired in
            }
        }

        /// <summary>
        /// The pins of a shape of Detection: only its own — Detection looks by its own interval and writes its own variable,
        /// so the interval and the variable every sensor has are no pins of a shape (a pin that does nothing would lie).
        /// </summary>
        public static IEnumerable<FieldInfo> ShapePins(SensorTask shape)
        {
            if (shape == null)
                yield break;

            foreach (FieldInfo field in Pins(shape))
            {
                if (field.DeclaringType != typeof(SensorTask))
                    yield return field;
            }
        }

        /// <summary>
        /// The link fields of a node that take value nodes by a wire (step 90): one value (a Get into a Compare, a What into
        /// a search) or a list of them (the Abilities of Use Any Ability) — each an input pin of the node.
        /// </summary>
        public static IEnumerable<GraphFields.Link> ValueLinks(AgentNode node)
        {
            if (node == null)
                yield break;

            foreach (GraphFields.Link link in GraphFields.LinksOf(node.GetType()))
            {
                if (!link.IsObject && typeof(AgentValue).IsAssignableFrom(link.ElementType) && !IsOutput(link))
                    yield return link;
            }
        }

        /// <summary>
        /// The link fields of a block that hold the values it gives out (step 91: Component and Found of Try Get Component)
        /// — each an output data pin of the block.
        /// </summary>
        public static IEnumerable<GraphFields.Link> OutputLinks(AgentNode node)
        {
            if (node == null)
                yield break;

            foreach (GraphFields.Link link in GraphFields.LinksOf(node.GetType()))
            {
                if (!link.IsObject && !link.IsList && typeof(AgentValue).IsAssignableFrom(link.ElementType) && IsOutput(link))
                    yield return link;
            }
        }

        private static bool IsOutput(GraphFields.Link link) => link.Field.IsDefined(typeof(ValueOutputAttribute), false);

        /// <summary>
        /// The link fields of a node that take conditions by a wire: a Branch's question, a decision's Condition (step 90),
        /// the inputs of AND / OR — each a Condition pin of the node.
        /// </summary>
        public static IEnumerable<GraphFields.Link> ConditionLinks(AgentNode node)
        {
            if (node == null)
                yield break;

            foreach (GraphFields.Link link in GraphFields.LinksOf(node.GetType()))
            {
                if (!link.IsObject && typeof(AgentCondition).IsAssignableFrom(link.ElementType))
                    yield return link;
            }
        }

        /// <summary>
        /// The link fields of a node that point to a project asset (an ability, a weapon — [NonSerialized] public fields of
        /// a UnityEngine.Object type, kept in the asset's Objects table): each drawn as a picker of that asset type.
        /// </summary>
        public static IEnumerable<GraphFields.Link> AssetLinks(AgentNode node)
        {
            if (node == null)
                yield break;

            foreach (GraphFields.Link link in GraphFields.LinksOf(node.GetType()))
            {
                if (link.IsObject && !link.IsList)
                    yield return link;
            }
        }

        /// <summary>The variable a pin field reads now (empty — none).</summary>
        public static string VariableOf(object element, FieldInfo field)
        {
            object value = field.GetValue(element);
            return value is string name ? name : IsValue(field) && Uses(value) ? NameOf(value) : "";
        }

        /// <summary>What a wire into the pin of this field carries (pins and variable nodes of one kind fit each other).</summary>
        public static Type PinType(FieldInfo field) =>
            IsValue(field) ? PinType(KindOf(field.FieldType) ?? VariableType.Float)
            : field.GetCustomAttribute<VariableNameAttribute>() is { HasType: true } named ? PinType(named.Type)
            : typeof(Transform);

        public static Type PinType(VariableType type) => type switch
        {
            VariableType.Transform => typeof(Transform),
            VariableType.Bool => typeof(bool),
            _ => typeof(float)
        };
    }
}
