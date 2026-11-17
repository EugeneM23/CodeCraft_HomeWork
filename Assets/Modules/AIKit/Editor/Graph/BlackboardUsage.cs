using System;
using System.Collections.Generic;
using System.Reflection;

namespace AIKit.Editor
{
    /// <summary>One piece of data of a behavior: its name, its kind, and the nodes that write it and read it.</summary>
    public sealed class VariableUse
    {
        public string Name;

        /// <summary>The kind as its type: "Transform", "float", "int", "double", "bool", "string", "Vector3".</summary>
        public string Kind;

        /// <summary>The variable is declared in the graph (false — nodes only name it, as a counter).</summary>
        public bool Declared;

        public VariableType Type;
        /// <summary>The group the panel lists it in when it is not told by who reads it (a number of the character).</summary>
        public string Group;

        /// <summary>The value a number of the character has on the character itself.</summary>
        public float OwnValue;

        public readonly List<string> Writers = new();
        public readonly List<string> Readers = new();
    }

    /// <summary>
    /// What data a behavior graph keeps on the blackboard and who touches it: for every variable — the nodes that
    /// write it (a sensor's "Store In", a Set or an Add node) and the nodes that read it (a task's object, a condition, a number
    /// taken from a variable). The blackboard is the one channel of data between nodes and between brains (handbook
    /// §8), so this list is the answer to "where does this value come from and who depends on it".
    /// </summary>
    public static class BlackboardUsage
    {
        public static List<VariableUse> Collect(GraphDocument document)
        {
            var uses = new List<VariableUse>();
            if (document?.Graph == null)
                return uses;

            foreach (VariableDeclaration declaration in document.Graph.Variables)
            {
                VariableUse use = Find(uses, declaration.Name, declaration.Type);
                use.Declared = true;
                use.Type = declaration.Type;
                use.Kind = KindOf(declaration.Type);
            }

            foreach (SensorTask sensor in document.Sensors())
            {
                Scan(uses, sensor, $"Sensor {sensor.DisplayName}".Trim());
                if (sensor is DetectionTask detection)
                {
                    foreach (SensorTask shape in detection.Sensors)
                        Scan(uses, shape, "Detection");
                }
            }

            foreach (AgentState state in document.States())
            {
                foreach (AgentTask task in state.Tasks)
                {
                    if (task is not SensorTask)
                        Scan(uses, task, state.DisplayName);
                }

                foreach (Transition transition in state.Transitions)
                {
                    if (transition.Trigger == TransitionTrigger.Tick)
                        Note(uses, BehaviorGraphInstance.DELTA_TIME, VariableType.Float, state.DisplayName, true);
                }
            }

            // Delta Time is the graph's own number (seconds since the On Tick chain last ran): always on the blackboard,
            // so a chain can be built with it — not only after an On Tick wire already exists
            if (!uses.Exists(use => use.Name == BehaviorGraphInstance.DELTA_TIME))
                Note(uses, BehaviorGraphInstance.DELTA_TIME, VariableType.Float, "On Tick", true);

            return uses;
        }

        /// <summary>
        /// The numbers of the character as variables of the blackboard — none any more (step 90, spec 4.9: the numbers
        /// mirror of step 34 is gone; handbook §8.2 — no copies of settings on the blackboard). The character's numbers
        /// and states are read where they live: the Character section of the panel (CharacterSection), the Get and Is
        /// nodes. Kept, empty, for the callers that still ask.
        /// </summary>
        public static List<VariableUse> OfCharacter(AICharacter character) => new();

        /// <summary>
        /// The kind of a variable as its type (15.11.2026, the user's word: "write the type — bool, int, float"): "bool", "int",
        /// "float", "string", "Vector3", "Transform".
        /// </summary>
        public static string KindOf(VariableType type) => type switch
        {
            VariableType.Transform => "Transform",
            VariableType.Bool => "bool",
            VariableType.Int => "int",
            VariableType.Double => "double",
            VariableType.String => "string",
            VariableType.Vector3 => "Vector3",
            _ => "float"
        };

        private static void Scan(List<VariableUse> uses, object node, string who)
        {
            if (node == null)
                return;

            // The question of a Branch and the inputs of an AND / OR are read on behalf of their owner
            if (node is BranchTask { Condition: { } question })
                Scan(uses, question, who);
            if (node is AgentCondition condition && GraphDocument.InputsOf(condition) is { } inputs)
            {
                foreach (AgentCondition input in inputs)
                    Scan(uses, input, who);
            }

            // So is what comes in by a wire into a pin of its own (step 90): a decision's Condition, a value node in
            // Compare's A / B, a Set's or a Debug Log's Number — an Is or a Get reads the object wired into it
            foreach (FieldInfo link in node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                bool wiredIn = typeof(AgentValue).IsAssignableFrom(link.FieldType) ||
                               typeof(AgentCondition).IsAssignableFrom(link.FieldType) && node is not BranchTask;
                if (wiredIn && link.GetValue(node) is AgentNode wired)
                    Scan(uses, wired, who);
            }

            if (node is CompareCondition compared)
            {
                Note(uses, compared.Variable, compared.Kind, who, false);
                if (!string.IsNullOrEmpty(compared.Other))
                    Note(uses, compared.Other, VariableType.Float, who, false);
                return;
            }

            foreach (FieldInfo field in node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.FieldType == typeof(string) && field.GetCustomAttribute<VariableNameAttribute>() is { } named)
                {
                    VariableType type = named.HasType ? named.Type : VariableType.Float;
                    bool writes = field.Name == "StoreIn" || node is NumberTask || node is SetFlagTask;
                    Note(uses, field.GetValue(node) as string, type, who, writes);
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(BlackboardValue<>))
                {
                    object value = field.GetValue(node);
                    bool fromVariable = (bool)field.FieldType.GetProperty("UsesVariable").GetValue(value);
                    if (fromVariable)
                        Note(uses, field.FieldType.GetProperty("Name").GetValue(value) as string, VariableType.Float, who, false);
                }
            }
        }

        private static void Note(List<VariableUse> uses, string name, VariableType type, string who, bool writes)
        {
            if (string.IsNullOrEmpty(name))
                return;

            VariableUse use = Find(uses, name, type);
            List<string> list = writes ? use.Writers : use.Readers;
            if (!list.Contains(who))
                list.Add(who);
        }

        private static VariableUse Find(List<VariableUse> uses, string name, VariableType type)
        {
            foreach (VariableUse use in uses)
            {
                if (use.Name == name)
                    return use;
            }

            var added = new VariableUse { Name = name, Type = type, Kind = KindOf(type) };
            uses.Add(added);
            return added;
        }
    }
}
