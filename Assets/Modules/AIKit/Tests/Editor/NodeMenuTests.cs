using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    /// <summary>
    /// The one list of nodes (step 90, C1 / C9; spec 4.1): built from the nodes' own paths, offered the same for a right
    /// click, Space and a pin let go over an empty place (filtered by the pin); a shape of seeing goes into Detection, a
    /// sensor that works alone stands on its own; a graph's Brain is not offered twice, a squad's decisions only to a
    /// Squad Brain; every node sits in the tree of the spec.
    /// </summary>
    public sealed class NodeMenuTests
    {
        // The tree of spec 4.1 (with the renames of 10.10): where every node of the module stands in the list
        private static readonly Dictionary<string, string> Tree = new()
        {
            { "aikit.task.combat-brain", "AI/Brain/Brain" },
            { "aikit.task.squad-brain", "AI/Brain/Squad Brain" },
            { "aikit.reasoner.start", "AI/Decisions/Start" },
            { "aikit.reasoner.fight", "AI/Decisions/Combat/Attack" },
            { "aikit.reasoner.use-ability", "AI/Decisions/Combat/Use Ability" },
            { "aikit.reasoner.change-weapon", "AI/Decisions/Combat/Switch Weapon" },
            { "aikit.reasoner.kite", "AI/Decisions/Combat/Kite" },
            { "aikit.reasoner.charge", "AI/Decisions/Combat/Charge" },
            { "aikit.reasoner.notice", "AI/Decisions/Combat/Notice" },
            { "aikit.reasoner.watch", "AI/Decisions/Combat/Watch" },
            { "aikit.reasoner.use-any-ability", "AI/Decisions/Combat/Use Any Ability" },
            { "aikit.reasoner.target-attacks", "AI/Decisions/Reactions/Target Attacks" },
            { "aikit.reasoner.target-opens-up", "AI/Decisions/Reactions/Target Opens Up" },
            { "aikit.reasoner.got-hit", "AI/Decisions/Reactions/Got Hit" },
            { "aikit.reasoner.health-low", "AI/Decisions/Reactions/Health Low" },
            { "aikit.reasoner.enters-zone", "AI/Decisions/Reactions/Enters Zone" },
            { "aikit.sensor.hit-by", "AI/Senses/Hit By" },
            { "aikit.value.ability", "AI/Actions/Combat/Ability" },
            { "aikit.task.block", "AI/Actions/Combat/Block" },
            { "aikit.condition.attack-slot", "AI/Actions/Combat/Attack Slot" },
            { "aikit.condition.is", "Graph/Data/Is" },
            { "aikit.value.get", "Graph/Data/Get" },
            { "aikit.task.try-get-component", "Graph/Data/Try Get Component" },
            { "aikit.task.set-parameter", "AI/Actions/Life/Set Parameter" },
            { "aikit.reasoner.chase", "AI/Decisions/Movement/Chase" },
            { "aikit.reasoner.flank", "AI/Decisions/Movement/Flank" },
            { "aikit.reasoner.circle", "AI/Decisions/Movement/Circle" },
            { "aikit.reasoner.patrol", "AI/Decisions/Movement/Patrol" },
            { "aikit.reasoner.climb", "AI/Decisions/Movement/Climb" },
            { "aikit.reasoner.daily-routine", "AI/Decisions/Life/Daily Routine" },
            { "aikit.reasoner.sleep", "AI/Decisions/Life/Sleep" },
            { "aikit.reasoner.follow-squad", "AI/Decisions/Squad/Follow Squad" },
            { "aikit.reasoner.gather", "AI/Decisions/Squad/Gather" },
            { "aikit.reasoner.form-up", "AI/Decisions/Squad/Form Up" },
            { "aikit.reasoner.advance", "AI/Decisions/Squad/Advance" },
            { "aikit.reasoner.surround", "AI/Decisions/Squad/Surround" },
            { "aikit.reasoner.hold-the-line", "AI/Decisions/Squad/Hold Line" },
            { "aikit.sensor.detection", "AI/Senses/Detection" },
            { "aikit.sensor.radius", "AI/Senses/Radius" },
            { "aikit.sensor.radius-sight", "AI/Senses/Radius (Line Of Sight)" },
            { "aikit.sensor.cone", "AI/Senses/Cone" },
            { "aikit.sensor.zone", "AI/Senses/Zone" },
            { "aikit.sensor.calls-for-help", "AI/Senses/Calls For Help" },
            { "aikit.sensor.time", "AI/Senses/Time Of Day" },
            { "aikit.sensor.senses", "AI/Senses/Senses" },
            { "aikit.sensor.squad-sight", "AI/Senses/Squad Sight" },
            { "aikit.task.move-to", "AI/Actions/Movement/Move To" },
            { "aikit.task.chase", "AI/Actions/Movement/Chase" },
            { "aikit.task.flank", "AI/Actions/Movement/Flank" },
            { "aikit.task.kite", "AI/Actions/Movement/Kite" },
            { "aikit.task.patrol-area", "AI/Actions/Movement/Patrol Area" },
            { "aikit.task.circle-target", "AI/Actions/Movement/Circle Target" },
            { "aikit.task.square-off", "AI/Actions/Movement/Square Off" },
            { "aikit.task.turn-to-target", "AI/Actions/Movement/Turn To Target" },
            { "aikit.task.climb-to", "AI/Actions/Movement/Climb To" },
            { "aikit.task.stand", "AI/Actions/Movement/Stand" },
            { "aikit.task.step", "AI/Actions/Movement/Step" },
            { "aikit.task.dash", "AI/Actions/Movement/Dash" },
            { "aikit.task.attack", "AI/Actions/Combat/Attack" },
            { "aikit.task.use-ability", "AI/Actions/Combat/Use Ability" },
            { "aikit.task.switch-weapon", "AI/Actions/Combat/Switch Weapon" },
            { "aikit.task.take-attack-slot", "AI/Actions/Combat/Take Attack Slot" },
            { "aikit.task.release-attack-slot", "AI/Actions/Combat/Release Attack Slot" },
            { "aikit.task.charge", "AI/Actions/Combat/Charge" },
            { "aikit.task.call-for-help", "AI/Actions/Combat/Call For Help" },
            { "aikit.task.drop", "AI/Actions/Objects/Drop" },
            { "aikit.reasoner.search-for", "AI/Decisions/Objects/Search For" },
            { "aikit.value.what", "AI/Actions/Objects/What" },
            { "aikit.task.find", "AI/Actions/Objects/Find" },
            { "aikit.task.interact", "AI/Actions/Objects/Interact" },
            { "aikit.task.perform", "AI/Actions/Life/Perform" },
            { "aikit.task.chat", "AI/Actions/Life/Chat" },
            { "aikit.task.go-inside", "AI/Actions/Life/Go Inside" },
            { "aikit.task.keep-formation", "AI/Actions/Squad/Keep Formation" },
            { "aikit.task.march-to-target", "AI/Actions/Squad/March To Target" },
            { "aikit.task.form-ring", "AI/Actions/Squad/Form Ring" },
            { "aikit.task.hold-line", "AI/Actions/Squad/Hold Line" },
            { "aikit.task.return-to-banner", "AI/Actions/Squad/Return To Banner" },
            { "aikit.task.branch", "Graph/Flow/Branch" },
            { "aikit.task.random", "Graph/Flow/Random" },
            { "aikit.task.wait", "Graph/Flow/Wait" },
            { "aikit.condition.compare", "Graph/Conditions/Compare" },
            { "aikit.condition.all", "Graph/Conditions/AND" },
            { "aikit.condition.any", "Graph/Conditions/OR" },
            { "aikit.task.set-number", "Graph/Variables/Set" },
            { "aikit.task.add-number", "Graph/Variables/Add" },
            { "aikit.task.subtract-number", "Graph/Variables/Subtract" },
            { "aikit.task.multiply-number", "Graph/Variables/Multiply" },
            { "aikit.task.divide-number", "Graph/Variables/Divide" },
            { "aikit.task.set-flag", "Graph/Variables/Set Flag" },
            { "aikit.task.debug-log", "Graph/Debug/Debug Log" }
        };

        // Step 90 is merged: every node of the module stands at its place in the tree (nothing pending, nothing leaving)
        private static readonly HashSet<string> PendingPaths = new();

        private static readonly HashSet<string> Leaving = new();

        private BehaviorGraphAsset _asset;
        private GraphDocument _document;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _document = new GraphDocument(_asset);
            LogAssert.ignoreFailingMessages = true;     // half-made graphs: the validator names what is missing
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        // ---------- the tree ----------

        [Test]
        public void Tree_EveryOfferedNode_SitsInTheTreeOfTheSpec_EveryDecisionUnderAIDecisions()
        {
            var wrong = new List<string>();
            foreach (NodeMenu.Item item in NodeMenu.Everything(_document).Where(item => item.Act != NodeMenu.Act.Flag))
            {
                System.Type type = AIKitNodeRegistry.Find(item.Id);
                if (type.Assembly != typeof(AgentNode).Assembly || Leaving.Contains(item.Id))
                    continue;                           // a game's own node goes where its path says

                if (!Tree.TryGetValue(item.Id, out string expected))
                {
                    wrong.Add($"{item.Id}: not in the tree of the spec");
                    continue;
                }

                if (PendingPaths.Contains(item.Id))
                    continue;

                string path = ((AIKitNodeAttribute)System.Attribute.GetCustomAttribute(type, typeof(AIKitNodeAttribute))).Path;
                if (path != expected)
                    wrong.Add($"{item.Id}: '{path}', the tree says '{expected}'");
                if (item.Kind == NodeKind.Decision && !path.StartsWith("AI/Decisions/"))
                    wrong.Add($"{item.Id}: a decision outside AI/Decisions");
            }

            CollectionAssert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        [Test]
        public void Tree_EveryNodeItNames_IsInTheRegistry()
        {
            List<string> missing = Tree.Keys.Where(id => AIKitNodeRegistry.Find(id) == null).ToList();

            CollectionAssert.IsEmpty(missing, "The tree names nodes that are not there: " + string.Join(", ", missing));
        }

        [Test]
        public void Everything_FoldersInTheOrderOfTheSpec_NodesByTheirOrder()
        {
            List<string> paths = NodeMenu.Everything(_document).Select(item => item.Path).ToList();
            string[] tops = paths.Select(path => path.Split('/')[0]).Distinct().ToArray();
            string[] groups = paths.Select(path => path.Substring(0, path.LastIndexOf('/'))).Distinct().ToArray();

            CollectionAssert.AreEqual(new[] { "AI", "Graph" }, tops, "Two roots: what the AI thinks and does, the canvas's tools");
            int At(string group) => System.Array.IndexOf(groups, group);
            Assert.Less(At("AI/Decisions/Squad"), At("AI/Senses"));
            Assert.Less(At("AI/Senses"), At("AI/Actions/Movement"));
            Assert.Less(At("AI/Actions/Movement"), At("AI/Actions/Squad"));
            Assert.Less(At("Graph/Flow"), At("Graph/Conditions"));
            Assert.Less(At("Graph/Conditions"), At("Graph/Data"));
            Assert.Less(At("Graph/Variables"), At("Graph/Debug"));
            Assert.Less(paths.IndexOf("AI/Senses/Radius"), paths.IndexOf("AI/Senses/Cone"), "Radius (20) before Cone (40)");
            Assert.Less(paths.IndexOf("AI/Senses/Cone"), paths.IndexOf("AI/Senses/Time Of Day"), "Cone (40) before Time Of Day (70)");
            Assert.Less(paths.IndexOf("Graph/Flow/Random"), paths.IndexOf("Graph/Flow/Wait"), "Random (20) before Wait (30)");
            Assert.Less(paths.IndexOf("Graph/Conditions/AND"), paths.IndexOf("Graph/Conditions/OR"));
            Assert.Less(paths.FindIndex(path => path.StartsWith("AI/Brain/")), paths.FindIndex(path => path.StartsWith("AI/Decisions/")));
        }

        // ---------- what the whole list holds ----------

        [Test]
        public void Everything_EveryNodeOnce_NoCodeNodeNoTestNode()
        {
            List<NodeMenu.Item> items = NodeMenu.Everything(_document);

            List<string> twice = items.GroupBy(item => item.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            CollectionAssert.IsEmpty(twice, "Each node is offered once");
            Assert.IsFalse(items.Any(item => item.Id.StartsWith("aikit.tests.")), "The tests' own nodes are not offered");
            Assert.IsFalse(items.Any(item => item.Id == "aikit.sensor.detection"), "Detection comes with its first shape");
            foreach (AIKitNodeRegistry.Entry entry in AIKitNodeRegistry.Entries)
            {
                bool block = entry.Info.Category == "Task" && entry.Info.Simple && !entry.Id().StartsWith("aikit.tests.");
                if (block)
                    Assert.IsTrue(items.Any(item => item.Id == entry.Info.Id), $"{entry.Info.Id} is offered on a graph with no Brain yet");
            }
        }

        [Test]
        public void Everything_AGraphWithABrain_OffersNoSecondBrain_OnlyACharactersDecisions()
        {
            _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);

            List<NodeMenu.Item> items = NodeMenu.Everything(_document);

            Assert.IsFalse(items.Any(item => item.Kind == NodeKind.Brain), "One Brain per graph");
            Assert.IsTrue(items.Any(item => item.Id == "aikit.reasoner.follow-squad"), "A character's decision");
            Assert.IsFalse(items.Any(item => item.Id == "aikit.reasoner.gather"), "A squad's decision is never taken here");
            Assert.IsFalse(items.Any(item => item.Id == "aikit.task.form-ring"), "Nor a block that leads a formation");
            Assert.IsTrue(items.Any(item => item.Id == "aikit.task.keep-formation"), "A member keeps its place in one");
        }

        [Test]
        public void Everything_ASquadsCommandGraph_OffersOnlyASquadsDecisions()
        {
            _document.AddStateWithTask(null, "aikit.task.squad-brain", Vector2.zero);

            List<NodeMenu.Item> items = NodeMenu.Everything(_document);

            Assert.IsTrue(items.Any(item => item.Id == "aikit.reasoner.gather"));
            Assert.IsTrue(items.Any(item => item.Id == "aikit.task.form-ring"));
            Assert.IsFalse(items.Any(item => item.Id == "aikit.reasoner.follow-squad"), "A character's decision");
            Assert.IsFalse(items.Any(item => item.Kind == NodeKind.Brain));
        }

        [Test]
        public void Everything_ADecisionAndABlockOfOneName_AreToldApartByTheKind()
        {
            List<NodeMenu.Item> items = NodeMenu.Everything(_document);

            NodeMenu.Item decision = items.Single(item => item.Id == "aikit.reasoner.hold-the-line");
            NodeMenu.Item block = items.Single(item => item.Id == "aikit.task.hold-line");
            Assert.AreEqual("Hold Line (decision)", decision.Name, "The search shows names only: the kind tells them apart");
            Assert.AreEqual("Hold Line", block.Name);
            Assert.AreNotEqual(decision.Tint, block.Tint, "A tile of its kind's colour before each");
        }

        // ---------- sensors: a shape into Detection, a sensor that works alone on its own ----------

        [Test]
        public void Sensors_AShapeGoesIntoDetection_ASensorThatWorksAloneStandsOnItsOwn()
        {
            List<NodeMenu.Item> items = NodeMenu.Everything(_document);
            NodeMenu.Item radius = items.Single(item => item.Id == "aikit.sensor.radius");
            NodeMenu.Item clock = items.Single(item => item.Id == "aikit.sensor.time");
            NodeMenu.Item squad = items.Single(item => item.Id == "aikit.sensor.squad-sight");
            Assert.AreEqual(NodeMenu.Act.Shape, radius.Act);
            Assert.AreEqual(NodeMenu.Act.Sensor, clock.Act);
            Assert.AreEqual(NodeMenu.Act.Sensor, squad.Act);

            NodeMenu.Add(_document, clock, new Vector2(-400f, 0f));
            Assert.IsNull(_document.Detection(), "The clock is no shape of seeing: no Detection is made for it");
            Assert.IsTrue(_document.Sensors().OfType<TimeSensorTask>().Any(), "It stands on its own");

            NodeMenu.Add(_document, radius, new Vector2(-400f, 200f));
            Assert.IsNotNull(_document.Detection(), "A shape comes into Detection (made with the first one)");
            Assert.IsInstanceOf<RadiusSensorTask>(_document.Detection().Sensors.Single());
            Assert.IsFalse(_document.Detection().Sensors.OfType<TimeSensorTask>().Any());
            CollectionAssert.AreEquivalent(new[] { NodeMenu.Act.Shape },
                NodeMenu.Shapes().Select(item => item.Act).Distinct(), "The Sensors pin offers shapes only");
            Assert.IsFalse(NodeMenu.Shapes().Any(item => item.Id == "aikit.sensor.time"));
        }

        // ---------- a pin let go over an empty place ----------

        [Test]
        public void After_TheBrainsPin_OffersDecisions_ABlocksPin_Blocks_AnOnTickPin_InstantBlocks()
        {
            _document.AddStateWithTask(null, "aikit.task.combat-brain", Vector2.zero);

            List<NodeMenu.Item> fromBrain = NodeMenu.After(_document, fromBrain: true, instantOnly: false);
            List<NodeMenu.Item> fromBlock = NodeMenu.After(_document, fromBrain: false, instantOnly: false);
            List<NodeMenu.Item> onTick = NodeMenu.After(_document, fromBrain: false, instantOnly: true);

            Assert.IsTrue(fromBrain.Count > 0 && fromBrain.All(item => item.Kind == NodeKind.Decision));
            Assert.IsTrue(fromBlock.Count > 0 && fromBlock.All(item => item.Kind == NodeKind.Block));
            Assert.IsTrue(onTick.Any(item => item.Id == "aikit.task.set-flag"), "An instant block");
            Assert.IsFalse(onTick.Any(item => item.Id == "aikit.task.wait"), "Wait lasts: not on On Tick");
            Assert.IsTrue(onTick.All(item => AIKitNodeRegistry.Create(item.Id) is AgentTask { IsInstant: true }));
        }

        [Test]
        public void Conditions_TheConditionsAndTheFlags_AFlagIsAskedIsItOn()
        {
            _document.DeclareVariable("Alarm", VariableType.Bool);
            AgentState branch = _document.AddStateWithTask(null, "aikit.task.branch", Vector2.zero);

            List<NodeMenu.Item> items = NodeMenu.Conditions(_document);

            Assert.IsTrue(items.Any(item => item.Id == "aikit.condition.compare"));
            Assert.IsTrue(items.Any(item => item.Id == "aikit.condition.all" && item.Name == "AND"));
            NodeMenu.Item alarm = items.Single(item => item.Act == NodeMenu.Act.Flag);
            Assert.AreEqual("Graph/Conditions/Flags/Alarm", alarm.Path);
            Assert.IsTrue(items.All(item => item.Kind == NodeKind.Condition), "Only what answers a question");

            var flag = (CompareCondition)NodeMenu.Add(_document, alarm, new Vector2(-200f, 0f), branch.Tasks[0]);
            Assert.AreSame(flag, ((BranchTask)branch.Tasks[0]).Condition, "Into the Branch that asks");
            Assert.AreEqual("Alarm", flag.Variable);
            Assert.AreEqual(VariableType.Bool, flag.Kind);
        }

        [Test]
        public void Add_ANode_StandsWhereTheListWasOpened()
        {
            NodeMenu.Item wait = NodeMenu.Everything(_document).Single(item => item.Id == "aikit.task.wait");

            var added = (AgentState)NodeMenu.Add(_document, wait, new Vector2(120f, 80f));

            Assert.IsInstanceOf<WaitTask>(added.Tasks.Single());
            Assert.AreEqual(new Vector2(120f, 80f), _document.PositionOf(added));
        }
    }

    internal static class RegistryEntryIds
    {
        public static string Id(this AIKitNodeRegistry.Entry entry) => entry.Info.Id;
    }
}
