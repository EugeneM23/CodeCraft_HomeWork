using System.Collections.Generic;
using AIKit.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Step 90's migrations of the objects lane, without the lab: an old graph with the tactics nodes of step 87 is read
    /// into the new picture (Search For; What → Find → Move To → Interact; Throw → Attack); a graph's object variables the
    /// characters used to fill (Point A, Point B, Patrol Zone) go with their wires; a character's scene objects become AI
    /// Objects that name it among their owners.
    /// </summary>
    public sealed class MigrationTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        // ---------- the old tactics nodes (a runtime upgrade step) ----------

        [Test]
        public void OldGraph_FetchAndPickUp_ReadAsSearchFor_WhatFindMoveToInteract_TheArrowsWhereTheyWere()
        {
            BehaviorGraphAsset asset = OldGraph(out int decision, out int block, out AgentTask after, out AgentTask failed);
            Rewrite(asset, decision, TacticsNodesUpgrade.FETCH_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":2,\"NotFrom\":[],\"Kind\":\"Rock\",\"SearchRadius\":25.0}");
            Rewrite(asset, block, TacticsNodesUpgrade.PICK_UP,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Kind\":\"Rock\"," +
                "\"SearchRadius\":{\"_useVariable\":false,\"_scope\":0,\"_name\":\"\",\"_constant\":25.0}," +
                "\"Pace\":{\"_useVariable\":false,\"_scope\":0,\"_name\":\"\",\"_constant\":0.8},\"Action\":\"\"}");

            var issues = new List<GraphIssue>();
            BehaviorGraph graph = GraphSerializer.Read(asset, issues);

            Assert.IsNotNull(graph);
            Assert.IsTrue(asset.UpgradedOnRead, "Upgraded when read");
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.UnknownNodeType),
                "Nothing left unread: the graph opens for editing");
            Assert.IsFalse(issues.Exists(issue => issue.IsError), string.Join("\n", issues));
            Assert.AreEqual(2, issues.FindAll(issue => issue.Code == GraphIssueCode.ReplacedNode).Count,
                "Each replaced node says what took its place");

            AgentState decisionNode = graph.Brain.Transitions[0].Target;
            var search = decisionNode.Reasoner as SearchForReasoner;
            Assert.IsNotNull(search, "Fetch is Search For");
            Assert.AreEqual(2, search.Priority, "With its priority");

            AgentState findNode = decisionNode.Transitions[0].Target;
            var find = findNode.Tasks[0] as FindTask;
            Assert.IsNotNull(find, "Pick Up's node finds now");
            Assert.AreEqual("Rock", find.What.Tag);
            Assert.AreEqual(Offer.Take, find.What.Offer);
            Assert.AreEqual(25f, find.What.Radius, 1e-4f);
            AgentState moveNode = findNode.Transitions.Find(arrow => arrow.Trigger == TransitionTrigger.TasksSucceeded).Target;
            var move = moveNode.Tasks[0] as MoveToTask;
            Assert.IsNotNull(move, "Then Move To");
            Assert.AreEqual(find.StoreIn, move.Target, "To what Find found");
            Assert.AreEqual(0.8f, move.Pace.Get(null), 1e-4f, "At the old pace");
            AgentState interactNode = moveNode.Transitions[0].Target;
            var interact = interactNode.Tasks[0] as InteractTask;
            Assert.IsNotNull(interact, "Then Interact");
            Assert.AreEqual(Offer.Take, interact.Interaction);
            Assert.AreEqual(find.StoreIn, interact.Object);
            Assert.IsTrue(graph.Variables.Exists(variable => variable.Name == find.StoreIn &&
                                                             variable.Type == VariableType.Transform), "Its variable is declared");

            Assert.AreEqual(after.Id, interactNode.Transitions[0].Target.Tasks[0].Id, "The old Done leaves from Interact");
            Assert.IsTrue(findNode.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.TasksFailed &&
                                                               arrow.Target.Tasks[0].Id == failed.Id),
                "The old Nothing Found stays on Find");
        }

        [Test]
        public void OldGraph_TakeCoverAndThrow_ReadAsACoverSearch_AndAnAttack_ThenWrittenItIsTheNewFormat()
        {
            BehaviorGraphAsset asset = OldGraph(out int decision, out int block, out _, out _);
            Rewrite(asset, decision, ThrowUpgrade.THROW_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":1,\"NotFrom\":[]}");
            Rewrite(asset, block, TacticsNodesUpgrade.TAKE_COVER,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Target\":\"\",\"Kind\":\"Cover\"," +
                "\"MinDistance\":{\"_useVariable\":false,\"_scope\":0,\"_name\":\"\",\"_constant\":6.0}," +
                "\"MaxDistance\":{\"_useVariable\":false,\"_scope\":0,\"_name\":\"\",\"_constant\":16.0}}");

            BehaviorGraph graph = GraphSerializer.Read(asset, new List<GraphIssue>());
            AgentState decisionNode = graph.Brain.Transitions[0].Target;
            Assert.AreEqual("aikit.reasoner.fight", GraphSerializer.TypeIdOf(decisionNode.Reasoner), "Throw is Attack");
            var find = decisionNode.Transitions[0].Target.Tasks[0] as FindTask;
            Assert.IsNotNull(find);
            Assert.AreEqual(typeof(Cover), find.What.ComponentType, "A cover is an object of its own now");
            Assert.AreEqual(Offer.Occupy, find.What.Offer);
            Assert.IsTrue(find.What.FromTarget);
            Assert.AreEqual(6f, find.What.MinDistance, 1e-4f);
            Assert.AreEqual(16f, find.What.MaxDistance, 1e-4f);

            GraphSerializer.Write(graph, asset);
            Assert.AreEqual(GraphUpgrades.Current, asset.FormatVersion, "Written: the current format");
            var again = new List<GraphIssue>();
            BehaviorGraph reread = GraphSerializer.Read(asset, again);
            Assert.IsFalse(asset.UpgradedOnRead, "Nothing to upgrade any more");
            Assert.IsFalse(again.Exists(issue => issue.Code == GraphIssueCode.ReplacedNode));
            Assert.IsInstanceOf<FindTask>(reread.Brain.Transitions[0].Target.Transitions[0].Target.Tasks[0]);
            Assert.IsNotNull(((FindTask)reread.Brain.Transitions[0].Target.Transitions[0].Target.Tasks[0]).What,
                "Its What is a node of the graph, wired in");
        }

        [Test]
        public void ThrowBlock_ReadAsAttack_ItsTargetKept()
        {
            BehaviorGraphAsset asset = OldGraph(out _, out int block, out _, out _);
            Rewrite(asset, block, ThrowUpgrade.THROW_BLOCK,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Target\":\"Enemy\",\"Action\":\"Throw\"}");

            BehaviorGraph graph = GraphSerializer.Read(asset, new List<GraphIssue>());
            var attack = graph.Brain.Transitions[0].Target.Transitions[0].Target.Tasks[0] as AttackTask;

            Assert.IsNotNull(attack, "The weapon in the hand throws");
            Assert.AreEqual("Enemy", attack.Target);
        }

        // ---------- the variables the characters filled (an editor upgrade step) ----------

        [Test]
        public void SceneObjectVariables_NothingWritesThem_TheyGoWithTheirNodesAndWires_AWrittenOneStays()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Point A", VariableType.Transform);
            g.Variable("Point B", VariableType.Transform);
            g.Variable("Patrol Zone", VariableType.Transform);
            g.Variable("Target", VariableType.Transform);
            var patrol = new PatrolAreaTask { Area = "Patrol Zone" };
            var climb = new ClimbToTask { Target = "Patrol Zone" };
            g.Chain(g.Decision(new PatrolReasoner(), 1), climb, patrol);
            g.Sensor(new RadiusSensorTask { StoreIn = "Target" });
            BehaviorGraph graph = g.Build();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            asset.FormatVersion = 0;                    // written before step 90: the characters filled these then
            asset.VariableNodes.Add(new GraphVariableNode { Variable = "Patrol Zone", Readers = { patrol.Id } });
            asset.VariableNodes.Add(new GraphVariableNode { Variable = "Target" });

            bool changed = new SceneObjectVariablesUpgrade().Apply(asset, graph, new List<GraphIssue>());

            Assert.IsTrue(changed);
            Assert.That(graph.Variables.ConvertAll(variable => variable.Name), Is.EquivalentTo(new[] { "Target" }),
                "Point A, Point B, Patrol Zone go; what a sensor writes stays");
            Assert.AreEqual("", patrol.Area, "Patrol Area has no area now: it walks round the home");
            Assert.AreEqual("", climb.Target);
            Assert.IsFalse(asset.VariableNodes.Exists(node => node.Variable == "Patrol Zone"), "Its node on the canvas goes");
            Assert.IsTrue(asset.VariableNodes.Exists(node => node.Variable == "Target"));
            Assert.IsFalse(new SceneObjectVariablesUpgrade().Apply(asset, graph, new List<GraphIssue>()),
                "Again: nothing to do");
        }

        [Test]
        public void SceneObjectVariables_WrittenByASearch_Stay()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Patrol Zone", VariableType.Transform);
            g.Chain(g.Decision(new PatrolReasoner(), 1),
                new FindTask { What = new WhatValue { ObjectKind = "Patrol Zone" }, StoreIn = "Patrol Zone" },
                new PatrolAreaTask { Area = "Patrol Zone" });
            BehaviorGraph graph = g.Build();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            asset.FormatVersion = 0;                    // written before step 90

            Assert.IsFalse(new SceneObjectVariablesUpgrade().Apply(asset, graph, new List<GraphIssue>()),
                "A Find fills it: it is the graph's own");
            Assert.IsTrue(graph.Variables.Exists(variable => variable.Name == "Patrol Zone"));
        }

        // ---------- a character's scene objects (an editor migration) ----------

        [Test]
        public void Bindings_BecomeAIObjectsOwnedByTheCharacter_TheUnreadOnesDropped_TheBindingsCleared()
        {
            AICharacter first = Character("First");
            AICharacter second = Character("Second");
            Transform home = Thing("Home 3");
            Transform well = Thing("Well");
            Transform point = Thing("Point");
            Bind(first, ("Home 3", home), ("Well", well), ("Point A", point));
            Bind(second, ("Well", well));

            int moved = SceneObjectsMigration.Migrate(first) + SceneObjectsMigration.Migrate(second);

            Assert.AreEqual(3, moved);
            var house = home.GetComponent<AIObject>();
            Assert.IsNotNull(house, "The house is an AI Object now");
            Assert.AreEqual("Home 3", house.Kind, "Of the variable's name: a Find of that kind, mine, finds it");
            Assert.That(house.Owners, Is.EquivalentTo(new[] { first }));
            Assert.That(well.GetComponent<AIObject>().Owners, Is.EquivalentTo(new[] { first, second }),
                "A place bound by two is both of theirs");
            Assert.IsNull(point.GetComponent<AIObject>(), "Point A: nobody reads it — no AI Object");
            Assert.AreEqual(0, Bindings(first), "The bindings are cleared");
            Assert.AreEqual(0, Bindings(second));
            Assert.AreEqual(0, SceneObjectsMigration.Migrate(first), "Again: nothing to do");
        }

        // ---------- helpers ----------

        // A graph written the old way: a decision (Daily Routine) with a block (Wait) behind its Do, Done → a Debug Log,
        // Failed → another Debug Log; the records of the decision's and the block's tasks are given back for rewriting
        private BehaviorGraphAsset OldGraph(out int decision, out int block, out AgentTask after, out AgentTask failed)
        {
            var g = new BrainGraphBuilder();
            var routine = new DailyRoutineReasoner();
            var wait = new WaitTask(1f);
            after = new DebugLogTask();
            failed = new DebugLogTask();
            AgentState chosen = g.Decision(routine, 2);
            AgentState waiting = g.Block(wait);
            g.Do(chosen, waiting);
            g.Then(waiting, g.Block(after));
            g.OnFail(waiting, g.Block(failed));
            BehaviorGraph graph = g.Build();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            asset.FormatVersion = 0;                    // before step 90
            decision = routine.Id;
            block = wait.Id;
            return asset;
        }

        private static void Rewrite(BehaviorGraphAsset asset, int id, string type, string data)
        {
            GraphRecord record = asset.Find(id);
            record.Type = type;
            record.Data = data.Replace("\"Id\":0", $"\"Id\":{id}");
        }

        private AICharacter Character(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            _created.Add(go);
            return go.AddComponent<AICharacter>();
        }

        private Transform Thing(string name)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            return go.transform;
        }

        // The legacy bindings, as a scene of before step 90 holds them
        private static void Bind(AICharacter character, params (string Variable, Transform Object)[] bindings)
        {
            var fields = new SerializedObject(character);
            SerializedProperty list = fields.FindProperty("_bindings");
            list.arraySize = bindings.Length;
            for (int i = 0; i < bindings.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative(nameof(ObjectBinding.Variable)).stringValue = bindings[i].Variable;
                entry.FindPropertyRelative(nameof(ObjectBinding.Object)).objectReferenceValue = bindings[i].Object;
            }

            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int Bindings(AICharacter character) =>
            new SerializedObject(character).FindProperty("_bindings").arraySize;
    }
}
