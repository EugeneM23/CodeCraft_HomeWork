using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 3c, nodes as data: every node of the module is a serializable type with public fields and a parameterless
    /// constructor, is in the registry under a unique stable id, and a node made with "new T()" (what a graph asset
    /// does) gets the same numbers as the convenience constructor's defaults — the bug §36 names: a constructor that
    /// assigns fields hides field defaults that differ from it — and its lists of nodes ready to be filled.
    /// </summary>
    public class NodeDataTests
    {
        /// <summary>A block that holds the agent for as long as its decision does.</summary>
        private sealed class Hold : AgentTask
        {
            protected override TaskStatus OnTick(AgentContext context, float dt) => TaskStatus.Running;
        }

        private static IEnumerable<Type> ModuleNodes() =>
            typeof(AgentNode).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(AgentNode).IsAssignableFrom(type))
                .OrderBy(type => type.Name);

        [Test]
        public void EveryNode_IsMarked_AndInTheRegistry()
        {
            var missing = new List<string>();
            foreach (Type type in ModuleNodes())
            {
                var info = type.GetCustomAttribute<AIKitNodeAttribute>();
                if (info == null)
                    missing.Add($"{type.Name}: no [AIKitNode]");
                else if (AIKitNodeRegistry.Find(info.Id) != type)
                    missing.Add($"{type.Name}: not found in the registry by '{info.Id}'");
            }

            CollectionAssert.IsEmpty(missing);
        }


        [Test]
        public void EveryTextSetting_IsPickedFromAList_OrNamesAVariable_OrIsFreeText()
        {
            // Step 90, C5 (handbook §8.1, §32.2): a key is picked from a list, never typed — a typo is seen while editing
            var typed = new List<string>();
            foreach (Type type in ModuleNodes())
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (field.FieldType != typeof(string) || field.IsNotSerialized || field.DeclaringType == typeof(AgentNode))
                        continue;                       // Name and Label: what the node is called, not a setting

                    bool picked = field.IsDefined(typeof(VariableNameAttribute), true) ||
                                  field.IsDefined(typeof(FreeTextAttribute), true) ||
                                  field.IsDefined(typeof(TagNameAttribute), true) ||
                                  field.IsDefined(typeof(ObjectKindAttribute), true) ||
                                  field.IsDefined(typeof(ActionNameAttribute), true) ||
                                  field.IsDefined(typeof(PoolNameAttribute), true) ||
                                  field.IsDefined(typeof(WeaponMoveNameAttribute), true) ||
                                  field.IsDefined(typeof(WeaponNameAttribute), true) ||
                                  field.IsDefined(typeof(TypeNameAttribute), true) ||
                                  field.IsDefined(typeof(MemberNameAttribute), true);
                    string name = $"{type.Name}.{field.Name}";
                    if (!picked)
                        typed.Add(name);
                }
            }

            CollectionAssert.IsEmpty(typed, "Text settings typed by hand: " + string.Join(", ", typed));
        }

        [Test]
        public void EveryDataNode_IsSerializable_WithAParameterlessConstructor()
        {
            var problems = new List<string>();
            foreach (Type type in ModuleNodes())
            {
                var info = type.GetCustomAttribute<AIKitNodeAttribute>();
                if (info != null && info.Category == AIKitNodeAttribute.CODE)
                    continue;               // a node of the Code category is code, not data

                if (!type.IsSerializable)
                    problems.Add($"{type.Name}: not [Serializable]");

                if (type.GetConstructor(Type.EmptyTypes) == null)
                    problems.Add($"{type.Name}: no parameterless constructor");

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance |
                                                                     BindingFlags.DeclaredOnly))
                {
                    if (property.CanWrite && property.GetSetMethod() != null)
                        problems.Add($"{type.Name}.{property.Name}: a settable property — data is a public field");
                }
            }

            CollectionAssert.IsEmpty(problems);
        }

        [Test]
        public void FieldDefaults_EqualTheConvenienceConstructorDefaults()
        {
            var problems = new List<string>();
            foreach (Type type in ModuleNodes())
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                    continue;               // reported by the test above

                object fresh = Activator.CreateInstance(type);
                foreach (ConstructorInfo constructor in type.GetConstructors())
                {
                    foreach (ParameterInfo parameter in constructor.GetParameters())
                    {
                        if (!parameter.HasDefaultValue)
                            continue;

                        FieldInfo field = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(f => string.Equals(f.Name, parameter.Name,
                                StringComparison.OrdinalIgnoreCase));
                        if (field == null)
                            continue;

                        object value = field.GetValue(fresh);
                        object expected = parameter.DefaultValue;
                        Type fieldType = field.FieldType;
                        if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(BlackboardValue<>))
                            value = fieldType.GetMethod("Get").Invoke(value, new object[] { null });   // its constant
                        if (value is IConvertible && expected is IConvertible && !(value is Enum))
                        {
                            if (Math.Abs(Convert.ToDouble(value) - Convert.ToDouble(expected)) > 1e-6)
                                problems.Add($"{type.Name}.{field.Name}: new() gives {value}, the constructor {expected}");
                        }
                        else if (!Equals(value, expected) && !(value is Enum e && Convert.ToInt32(e) ==
                                     Convert.ToInt32(expected)))
                        {
                            problems.Add($"{type.Name}.{field.Name}: new() gives {value}, the constructor {expected}");
                        }
                    }
                }
            }

            CollectionAssert.IsEmpty(problems);
        }

        [Test]
        public void Build_GivesEveryNodeAStableId_AndEdgesTheirTargetId()
        {
            BehaviorGraph Build()
            {
                var g = new BrainGraphBuilder();
                g.Sensor(new DetectionTask(new RadiusSensorTask(), new ConeSensorTask()) { StoreIn = "Target" });
                g.BrainTask.Target = "Target";
                AgentState watch = g.Decision(new WatchReasoner());
                g.Chain(watch, new BranchTask(new AllCondition(new CompareCondition("Target", VariableType.Transform))),
                    new WaitTask(1f));
                g.Do(g.Always(), g.Block(new WaitTask(1f), "Idle"));
                return g.Build();
            }

            BehaviorGraph first = Build();
            BehaviorGraph second = Build();
            var firstNodes = new List<AgentNode>();
            var secondNodes = new List<AgentNode>();
            first.VisitNodes(firstNodes.Add);
            second.VisitNodes(secondNodes.Add);

            Assert.AreEqual(firstNodes.Count, secondNodes.Count);
            var ids = new HashSet<int>();
            for (int i = 0; i < firstNodes.Count; i++)
            {
                Assert.AreNotEqual(0, firstNodes[i].Id, $"{firstNodes[i].DisplayName} has an id");
                Assert.AreEqual(firstNodes[i].Id, secondNodes[i].Id, "The same build gives the same ids");
                Assert.IsTrue(ids.Add(firstNodes[i].Id),
                    "Ids are unique in a graph: the nodes, their blocks, the shapes and conditions those hold");
            }

            var states = new List<AgentState>();
            first.CollectStates(states);
            List<Transition> edges = states.SelectMany(state => state.Transitions).ToList();
            Assert.IsNotEmpty(edges);
            foreach (Transition edge in edges)
                Assert.AreEqual(edge.Target.Id, edge.TargetId);
        }

        [Test]
        public void Registry_CreatesANodeById()
        {
            string id = typeof(WaitTask).GetCustomAttribute<AIKitNodeAttribute>().Id;

            AgentNode node = AIKitNodeRegistry.Create(id);

            Assert.IsInstanceOf<WaitTask>(node);
            Assert.AreEqual(1f, ((WaitTask)node).Seconds.Get(null));
        }

        [Test]
        public void NodesThatHoldNodes_MadeWithNew_HaveTheirListsReady()
        {
            var problems = new List<string>();
            foreach (Type type in ModuleNodes())
            {
                if (type.GetConstructor(Type.EmptyTypes) == null)
                    continue;               // reported above

                object fresh = Activator.CreateInstance(type);
                foreach (GraphFields.Link link in GraphFields.LinksOf(type))
                {
                    if (link.IsList && link.Field.GetValue(fresh) == null)
                        problems.Add($"{type.Name}.{link.Name}: new() gives no list to wire nodes into");
                }
            }

            CollectionAssert.IsEmpty(problems);

            // What the canvas wires in goes straight into them
            var detection = new DetectionTask();
            detection.Sensors.Add(new RadiusSensorTask());
            var and = new AllCondition();
            and.Conditions.Add(new CompareCondition());
            Assert.AreEqual(1, detection.Sensors.Count);
            Assert.AreEqual(1, and.Conditions.Count);
        }

        [Test]
        public void EnterState_ById_FindsTheNode()
        {
            var rig = new GraphRig();
            var g = new BrainGraphBuilder();
            g.Do(g.Always(), g.Block(new Hold(), "A"));
            AgentState sleep = g.Decision(new SleepReasoner { Condition = new CompareCondition("Night", VariableType.Transform) });      // never holds: nothing is night
            AgentState b = g.Block(new Hold(), "B");
            g.Do(sleep, b);
            AgentState stray = g.Block(new Hold(), "Stray");
            rig.SetGraph(g.Build());
            rig.Step();

            Assert.IsTrue(rig.Instance.EnterState(b.Id, rig.Context));
            Assert.AreEqual("B", rig.StateName);
            Assert.AreSame(sleep, rig.Instance.Decision, "The decision whose chain it is runs it");
            Assert.IsFalse(rig.Instance.EnterState(stray.Id, rig.Context), "A node no chain reaches never runs");
            Assert.AreEqual("B", rig.StateName);
        }
    }
}
