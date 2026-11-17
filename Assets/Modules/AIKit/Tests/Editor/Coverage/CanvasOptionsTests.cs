using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIKit.Editor;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// What the canvas offers and how it paints and fills a node's body (no window): the lists a picker is drawn from are
    /// read once and kept (the kinds of AI Object too); the kind and the colour of every node type; the list with search
    /// groups the items by their folders; the lists of values, conditions and flags; what a node's body leaves to its
    /// pins; the words of a picker; which fields are pins and how a pin switches between a number and a variable.
    /// </summary>
    public sealed class CanvasOptionsTests
    {
        [TearDown]
        public void TearDown()
        {
            NodeOptions.Forget();
            ElementInspector.Compact = false;
            ElementInspector.Wired = false;
            ElementInspector.ValuesInPins = false;
        }

        // ---------- the lists are kept ----------

        [Test]
        public void NodeOptions_For_ObjectKind_IsCachedLikeTheOtherLists()
        {
            FieldInfo kind = typeof(WhatValue).GetField(nameof(WhatValue.ObjectKind));
            FieldInfo move = typeof(AttackTask).GetField(nameof(AttackTask.Move));

            Assert.AreSame(NodeOptions.For(move), NodeOptions.For(move), "Set-up: the moves are read once and kept");
            Assert.AreSame(NodeOptions.For(kind), NodeOptions.For(kind),
                "The kinds of AI Object too: no scan of the scene and the prefabs per drawing");
            List<string> before = NodeOptions.For(kind);
            NodeOptions.Forget();
            Assert.AreNotSame(before, NodeOptions.For(kind), "…until the project changes");
        }

        [Test]
        public void ObjectKinds_Known_SortedOnceNoEmpty()
        {
            var made = new List<GameObject>();
            try
            {
                foreach (string kind in new[] { "Well", "Home 3", "Well", "" })
                {
                    var go = new GameObject($"Place {kind}");
                    made.Add(go);
                    go.AddComponent<AIObject>().Kind = kind;
                }

                List<string> known = ObjectKinds.Known();

                Assert.AreEqual(1, known.Count(kind => kind == "Well"), "Each kind once");
                Assert.Less(known.IndexOf("Home 3"), known.IndexOf("Well"), "Sorted");
                Assert.IsTrue(known.IndexOf("Home 3") >= 0);
                CollectionAssert.DoesNotContain(known, "", "No empty kind");
            }
            finally
            {
                made.ForEach(Object.DestroyImmediate);
            }
        }

        // ---------- kinds and colours ----------

        [Test]
        public void NodeKinds_KindAndColourOfEveryType()
        {
            Assert.AreEqual(NodeKind.Brain, NodeKinds.Of(typeof(CombatBrainTask)));
            Assert.AreEqual(NodeKind.Brain, NodeKinds.Of(typeof(SquadBrainTask)));
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                NodeKind expected =
                    typeof(CombatBrainTask).IsAssignableFrom(entry.Type) ? NodeKind.Brain
                    : typeof(ReasonerTask).IsAssignableFrom(entry.Type) ? NodeKind.Decision
                    : typeof(SensorTask).IsAssignableFrom(entry.Type) ? NodeKind.Sensor
                    : typeof(AgentCondition).IsAssignableFrom(entry.Type) ? NodeKind.Condition
                    : typeof(AgentValue).IsAssignableFrom(entry.Type) ? NodeKind.Value
                    : NodeKind.Block;
                Assert.AreEqual(expected, NodeKinds.Of(entry.Type), entry.Type.Name);
            }

            Assert.AreEqual(NodeKind.Block, NodeKinds.Of(null));
            Assert.AreEqual(new Color(0.62f, 0.13f, 0.13f), NodeKinds.TitleColor(typeof(BranchTask)), "Branch is red");
            Assert.IsNull(NodeKinds.TitleColor(typeof(WaitTask)), "Flow keeps the plain title");
            Assert.AreEqual(new Color(0.25f, 0.25f, 0.25f), NodeKinds.Tint(typeof(WaitTask)), "…and a plain grey tile");
            Assert.AreEqual(new Color(0.12f, 0.32f, 0.55f), NodeKinds.TitleColor(typeof(ChaseTask)), "Movement is blue");
            Assert.AreEqual(new Color(0.6f, 0.33f, 0.08f), NodeKinds.TitleColor(typeof(AttackTask)), "Combat is orange");
            Assert.AreEqual("Movement", NodeKinds.FolderOf(typeof(ChaseTask)));
            Assert.AreEqual("", NodeKinds.FolderOf(typeof(string)), "No node: no folder");
            Assert.IsNull(NodeKinds.Word(NodeKind.Block));
            Assert.AreEqual("value", NodeKinds.Word(NodeKind.Value));
            Assert.AreEqual("decision", NodeKinds.Word(NodeKind.Decision));
        }

        // ---------- the list with search ----------

        [Test]
        public void NodeLibrary_CreateSearchTree_AGroupPerFolderOnce_EntriesAtTheirDepth()
        {
            var items = new List<NodeMenu.Item>
            {
                new("AI/Brain/Brain", "a", NodeKind.Brain, NodeMenu.Act.Node, Color.grey),
                new("AI/Decisions/Combat/Attack (decision)", "b", NodeKind.Decision, NodeMenu.Act.Node, Color.grey),
                new("Flow/Wait", "c", NodeKind.Block, NodeMenu.Act.Node, Color.grey)
            };
            NodeMenu.Item picked = default;
            NodeLibrary library = NodeLibrary.Of("Add Node", items, item => picked = item);
            try
            {
                List<SearchTreeEntry> tree = library.CreateSearchTree(new SearchWindowContext(Vector2.zero));

                var expected = new (string Name, int Level, bool Group)[]
                {
                    ("Add Node", 0, true), ("AI", 1, true), ("Brain", 2, true), ("Brain", 3, false), ("Decisions", 2, true),
                    ("Combat", 3, true), ("Attack (decision)", 4, false), ("Flow", 1, true), ("Wait", 2, false)
                };
                Assert.AreEqual(expected.Length, tree.Count);
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.AreEqual(expected[i].Name, tree[i].name, $"Entry {i}");
                    Assert.AreEqual(expected[i].Level, tree[i].level, $"Entry {i} ({tree[i].name})");
                    Assert.AreEqual(expected[i].Group, tree[i] is SearchTreeGroupEntry, $"Entry {i} ({tree[i].name})");
                }

                library.OnSelectEntry(tree[^1], new SearchWindowContext(Vector2.zero));
                Assert.AreEqual("c", picked.Id, "The picked entry hands its item back");
            }
            finally
            {
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void NodeMenu_Values_Conditions_Flags_NoDocument()
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            try
            {
                var document = new GraphDocument(asset);
                document.DeclareVariable("Alarm", VariableType.Bool);
                document.DeclareVariable("Doors/Open", VariableType.Bool);

                CollectionAssert.AreEqual(new[] { "aikit.value.what" }, NodeMenu.Values(typeof(WhatValue)).Select(item => item.Id));
                CollectionAssert.AreEqual(new[] { "aikit.value.ability" },
                    NodeMenu.Values(typeof(AbilityValue)).Select(item => item.Id));
                Assert.IsFalse(NodeMenu.Conditions(document, false).Any(item => item.Act == NodeMenu.Act.Flag), "Without flags");
                NodeMenu.Item doors = NodeMenu.Conditions(document).Single(item => item.Id == NodeMenu.FLAG + "Doors/Open");
                Assert.AreEqual("Graph/Conditions/Flags/Doors Open", doors.Path, "A slash of the name opens no sub-list");

                List<NodeMenu.Item> everything = NodeMenu.Everything(null);
                Assert.IsTrue(everything.Any(item => item.Id == "aikit.task.combat-brain"), "No graph: the Brain is offered");
                Assert.IsTrue(everything.Any(item => item.Id == "aikit.task.squad-brain"));
                Assert.IsFalse(everything.Any(item => item.Act == NodeMenu.Act.Flag), "No graph: no flags");

                var at = new Vector2(100f, 50f);
                NodeMenu.Item alarm = NodeMenu.Conditions(document).Single(item => item.Id == NodeMenu.FLAG + "Alarm");
                var flag = (CompareCondition)NodeMenu.Add(document, alarm, at);
                AgentState branch = document.States().Single(state => state.Tasks.FirstOrDefault() is BranchTask);
                Assert.AreSame(flag, ((BranchTask)branch.Tasks[0]).Condition, "A Branch of its own asks the flag");
                Assert.AreEqual("Alarm", flag.Variable);
                Assert.AreEqual(VariableType.Bool, flag.Kind);
                Assert.AreEqual(at, document.PositionOf(branch));
                Assert.AreEqual(at + new Vector2(-240f, 70f), document.PositionOfNode(flag));
                Assert.IsNull(NodeMenu.Add(null, alarm, at), "No document: nothing");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // ---------- a node's body ----------

        [Test]
        public void ElementInspector_Drawn_LeavesOutWhatTheCanvasShowsElsewhere()
        {
            static List<string> Drawn(object element, bool ownOnly = true) =>
                ElementInspector.Drawn(element, ownOnly).Select(field => field.Name).ToList();

            ElementInspector.ValuesInPins = true;
            CollectionAssert.IsEmpty(Drawn(new WaitTask()), "Its numbers are typed in its pins");
            ElementInspector.ValuesInPins = false;
            CollectionAssert.AreEqual(new[] { nameof(WaitTask.Seconds), nameof(WaitTask.SecondsMax) }, Drawn(new WaitTask()));

            ElementInspector.Compact = true;
            ElementInspector.Wired = true;
            List<string> attack = Drawn(new AttackTask());
            CollectionAssert.Contains(attack, nameof(AttackTask.Move));
            CollectionAssert.DoesNotContain(attack, nameof(AttackTask.Target), "An object comes only by its wire");
            List<string> routine = Drawn(new DailyRoutineReasoner());
            CollectionAssert.DoesNotContain(routine, nameof(ReasonerTask.Priority), "Above the node, not in it");
            CollectionAssert.DoesNotContain(routine, nameof(ReasonerTask.NotFrom));
            CollectionAssert.DoesNotContain(routine, nameof(ReasonerTask.Condition), "A pin");

            CollectionAssert.AreEqual(new[] { nameof(AttackTask.Target) },
                ElementInspector.ObjectFields(new AttackTask()).Select(field => field.Name));
            CollectionAssert.IsEmpty(ElementInspector.ObjectFields(new WaitTask()));
        }

        [Test]
        public void ElementInspector_OptionWords_EmptyAndDuplicateOptions_ListedOnce()
        {
            List<string> words = ElementInspector.OptionWords(new[] { "", "Heavy", "Heavy" }, null, null, null,
                out int selected, out List<string> values);

            CollectionAssert.AreEqual(new[] { "(none)", "Heavy" }, words);
            CollectionAssert.AreEqual(new[] { "", "Heavy" }, values);
            Assert.AreEqual(0, selected);
        }

        // ---------- pins ----------

        private sealed class WholeNumbers
        {
            public BlackboardValue<int> Count = 3;
        }

        [Test]
        public void ValueFields_Pins_AndConversions()
        {
            static List<string> Pins(object element) => ValueFields.Pins(element).Select(field => field.Name).ToList();

            CollectionAssert.AreEqual(new[] { nameof(SetFlagTask.Variable) }, Pins(new SetFlagTask()));
            CollectionAssert.AreEqual(new[] { nameof(DebugLogTask.Value) }, Pins(new DebugLogTask()), "Not its message");
            CollectionAssert.Contains(Pins(new AttackTask()), nameof(AttackTask.Target));
            CollectionAssert.DoesNotContain(Pins(new AttackTask()), nameof(AttackTask.Move), "A move is picked, not wired");
            CollectionAssert.AreEqual(new[] { nameof(WaitTask.Seconds), nameof(WaitTask.SecondsMax) }, Pins(new WaitTask()));
            CollectionAssert.DoesNotContain(ValueFields.ShapePins(new RadiusSensorTask()).Select(field => field.Name).ToList(),
                nameof(SensorTask.Interval), "A shape's pins are its own");

            object boxed = (BlackboardValue<float>)2f;
            ValueFields.ToVariable(boxed, "X");
            Assert.IsTrue(ValueFields.Uses(boxed));
            Assert.AreEqual("X", ValueFields.NameOf(boxed));
            Assert.AreEqual(BlackboardScope.Agent,
                boxed.GetType().GetField("_scope", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boxed));
            ValueFields.ToConstant(boxed);
            Assert.IsFalse(ValueFields.Uses(boxed));
            Assert.AreEqual(2f, ValueFields.Constant(boxed), "Its own number kept");
            ValueFields.ToConstant(boxed, "text");
            Assert.AreEqual(2f, ValueFields.Constant(boxed), "A value of another kind is not taken");

            FieldInfo count = typeof(WholeNumbers).GetField(nameof(WholeNumbers.Count));
            Assert.AreEqual(typeof(float), ValueFields.PinType(count), "A whole number wires as a number");
            Assert.AreEqual("", ValueFields.VariableOf(new WholeNumbers(), count), "Not bound: no variable");
            Assert.AreEqual("", ValueFields.VariableOf(new AttackTask(), typeof(AttackTask).GetField(nameof(AttackTask.Target))));
        }
    }
}
