using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// Every node the registry knows, one by one: its settings survive a save and a load, a task runs on an agent that
    /// has nothing (no target, no variables, no world set up) without an error, and a condition answers there. A new
    /// node is covered by these the moment it carries [AIKitNode].
    /// </summary>
    public sealed class EveryNodeTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
            GraphRig.DestroyAll();
        }

        private static IEnumerable<AIKitNodeRegistry.Entry> Nodes(string category) =>
            AIKitNodeRegistry.Entries.Where(entry => entry.Info.Category == category);

        [Test]
        public void EveryNode_WithItsSettingsChanged_IsReadBackTheSame()
        {
            // the changed names name variables nobody declares: the validator says so, the test is about the data
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            var failures = new List<string>();
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (entry.Info.Category == AIKitNodeAttribute.CODE || entry.Info.Category == "State")
                    continue;

                AgentNode node = AIKitNodeRegistry.Create(entry.Info.Id);
                Change(node);
                AgentNode read = RoundTrip(node, out string problem);
                string before = JsonUtility.ToJson(node);      // after the write: the id is given then
                if (read == null)
                {
                    failures.Add($"{entry.Info.Id}: {problem}");
                    continue;
                }

                string after = JsonUtility.ToJson(read);
                if (before != after)
                    failures.Add($"{entry.Info.Id}:\n  wrote {before}\n  read  {after}");
            }

            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void EveryTask_OnAnAgentWithNothing_EntersRunsAndLeavesWithoutAnError()
        {
            var failures = new List<string>();
            foreach (AIKitNodeRegistry.Entry entry in Nodes("Task").Concat(Nodes("Sensor")))
            {
                var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
                try
                {
                    rig.SetGraph(GraphOf((AgentTask)AIKitNodeRegistry.Create(entry.Info.Id)));
                    rig.Step(0.05f, 40);
                    rig.Instance.Stop(rig.Context);
                }
                catch (Exception error)
                {
                    failures.Add($"{entry.Info.Id}: {error.GetType().Name}: {error.Message}");
                }
            }

            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void EveryCondition_OnAnAgentWithNothing_AnswersWithoutAnError()
        {
            var failures = new List<string>();
            var rig = new GraphRig(trace: false);
            foreach (AIKitNodeRegistry.Entry entry in Nodes("Condition"))
            {
                try
                {
                    var condition = (AgentCondition)AIKitNodeRegistry.Create(entry.Info.Id);
                    condition.Check(rig.Context);
                    condition.Check(null);
                }
                catch (Exception error)
                {
                    failures.Add($"{entry.Info.Id}: {error.GetType().Name}: {error.Message}");
                }
            }

            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void EveryNode_HasAPlainName_AndAStableIdOfItsOwn()
        {
            var ids = new HashSet<string>();
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (entry.Info.Category == AIKitNodeAttribute.CODE || !entry.Info.Simple && entry.Info.Category != "Sensor")
                    continue;                       // the canvas shows the simple nodes and the sensors only
                if (!entry.Type.Assembly.GetName().Name.StartsWith("AIKit"))
                    continue;                       // PORTING: the game's own nodes (game.*) are not the module's

                Assert.IsTrue(ids.Add(entry.Info.Id), $"Two nodes are saved under {entry.Info.Id}");
                StringAssert.StartsWith("aikit.", entry.Info.Id);
                string name = entry.Info.Path.Substring(entry.Info.Path.LastIndexOf('/') + 1);
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), entry.Info.Id);
                Assert.IsFalse(name.Contains("Task") || name.Contains("Reasoner") || name.Contains("Condition"),
                    $"{entry.Info.Id}: '{name}' — the canvas shows plain words, not class names");
            }
        }

        // ---------- helpers ----------

        // The task where its kind runs: a Brain as the graph's own Brain, a sensor among the graph's sensors, a decision
        // wired to the Brain (with a chain), any other block as the one block of a decision that always holds
        private static BehaviorGraph GraphOf(AgentTask task)
        {
            if (task is not CombatBrainTask && task is not SensorTask && task is not ReasonerTask)
                return GraphRig.OneState(task, "Only");

            var g = new BrainGraphBuilder(task as CombatBrainTask);
            if (task is SensorTask sensor)
                g.Sensor(sensor);
            if (task is ReasonerTask decision)
                g.Chain(g.Decision(decision, 1), new WaitTask(0.5f));
            g.Chain(g.Always(), new WaitTask(0.5f));
            return g.Build();
        }

        // Writes the node into a graph of its own (where its kind lives: a task in a node, a condition in a Branch) and
        // reads the graph back
        private AgentNode RoundTrip(AgentNode node, out string problem)
        {
            problem = null;
            var root = new AgentState("Root");
            var from = new AgentState("From");
            root.AddChild(from);
            switch (node)
            {
                case AgentCondition condition:
                    from.AddTask(new BranchTask(condition));
                    break;
                case AgentTask task:
                    from.AddTask(task);
                    break;
                case AgentValue value:
                    root.Values.Add(value);             // a value node nobody reads yet is kept by the root (step 90)
                    break;
                default:
                    problem = $"not a task, a condition or a value ({node.GetType().Name})";
                    return null;
            }

            var graph = new BehaviorGraph(root);
            graph.Link();
            graph.AssignIds();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            try
            {
                GraphSerializer.Write(graph, asset);
            }
            catch (Exception error)
            {
                problem = $"write: {error.Message}";
                return null;
            }

            BehaviorGraph read = asset.Compile();
            if (read == null)
            {
                problem = "compile: " + string.Join(" | ", asset.Issues);
                return null;
            }

            AgentNode found = null;
            read.VisitNodes(each =>
            {
                if (each.Id == node.Id)
                    found = each;
            });
            problem = found == null ? "not found after the load" : null;
            return found;
        }

        // A registered task with a link field (one or a list) that takes this value node, the value wired into it; null —
        // no node reads a value of this kind
        private static AgentTask HolderOf(AgentValue value)
        {
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                if (!typeof(AgentTask).IsAssignableFrom(entry.Type) || entry.Info.Category == AIKitNodeAttribute.CODE)
                    continue;

                foreach (GraphFields.Link link in GraphFields.LinksOf(entry.Type))
                {
                    if (link.IsObject || !link.ElementType.IsInstanceOfType(value))
                        continue;

                    var holder = (AgentTask)AIKitNodeRegistry.Create(entry.Info.Id);
                    if (link.IsList)
                    {
                        var list = (System.Collections.IList)(link.Field.GetValue(holder) ??
                                                              Activator.CreateInstance(link.Field.FieldType));
                        list.Add(value);
                        link.Field.SetValue(holder, list);
                    }
                    else
                    {
                        link.Field.SetValue(holder, value);
                    }

                    return holder;
                }
            }

            return null;
        }

        // Every plain setting gets a value no default has, so a field that is not saved shows up
        private static void Change(AgentNode node)
        {
            foreach (FieldInfo field in node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (field.IsInitOnly || field.IsNotSerialized || field.Name == nameof(AgentNode.Id))
                    continue;

                Type type = field.FieldType;
                object value = field.GetValue(node);
                if (type == typeof(float))
                    field.SetValue(node, (float)value + 1.25f);
                else if (type == typeof(int))
                    field.SetValue(node, (int)value + 2);
                else if (type == typeof(bool))
                    field.SetValue(node, !(bool)value);
                else if (type == typeof(string))
                    field.SetValue(node, field.Name + " X");
                else if (type == typeof(BlackboardValue<float>))
                    field.SetValue(node, (BlackboardValue<float>)(((BlackboardValue<float>)value).Get(null) + 1.5f));
                else if (type.IsEnum && Enum.GetValues(type).Length > 1)
                {
                    Array values = Enum.GetValues(type);
                    int at = Array.IndexOf(values, value);
                    field.SetValue(node, values.GetValue((at + 1) % values.Length));
                }
            }
        }
    }
}
