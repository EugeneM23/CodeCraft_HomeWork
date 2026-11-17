using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// A graph asset read, upgraded, compiled and opened (whole-plugin recheck, data): what cannot be read is an issue
    /// naming the record, never an exception; two records of one id — the second's links are not put on the first; the
    /// nodes an upgrade adds get ids of their own (never one of a record not read yet) and places next to the node they
    /// belong to; the notes of an upgrade stay on the graph the canvas saved; the variables an upgrade declares are
    /// declared for the checks too; an asset that cannot be read is read and reported once until it changes; a fresh
    /// asset's first layout gives sibling blocks places of their own.
    /// </summary>
    public sealed class DataGraphAssetTests
    {
        private const string TARGET = "Target";

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

        // ---------- what cannot be read ----------

        [Test]
        public void GraphSerializer_Read_ARecordWithMalformedData_IsAnIssueNamingIt_NotAnException()
        {
            var wait = new WaitTask(100f);
            BehaviorGraphAsset asset = Written(GraphRig.OneState(wait, "Idle"));
            asset.Find(wait.Id).Data = "{\"Seconds\":{\"_constant\":1.0}";        // a closing brace missing
            asset.MarkChanged();

            List<GraphIssue> issues = null;
            Assert.DoesNotThrow(() => issues = GraphValidator.Validate(asset));
            LogAssert.ignoreFailingMessages = true;                     // Compile logs the issue as an error
            try
            {
                Assert.DoesNotThrow(() => asset.Compile());
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.IsTrue(issues.Exists(issue => issue.IsError && issue.RecordId == wait.Id), string.Join("\n", issues));
            BehaviorGraph graph = GraphSerializer.Read(asset, new List<GraphIssue>());
            Assert.IsNotNull(graph?.Brain, "The rest is read: the Brain");
            Assert.IsTrue(graph.Brain.Transitions.Exists(wire => wire.Target?.Reasoner is DailyRoutineReasoner),
                "…and its decision");
        }

        [Test]
        public void GraphDocument_ARecordWithMalformedData_OpensReadOnly_NothingIsLostByAWrite()
        {
            var wait = new WaitTask(100f);
            BehaviorGraphAsset asset = Written(GraphRig.OneState(wait, "Idle"));
            const string BROKEN = "{\"Seconds\":{\"_constant\":1.0}";
            asset.Find(wait.Id).Data = BROKEN;
            asset.MarkChanged();

            var document = new GraphDocument(asset);

            Assert.IsTrue(document.IsReadOnly, "A record this code cannot read: shown, not edited");
            Assert.AreEqual(BROKEN, asset.Find(wait.Id).Data, "The record is kept for the user to fix");
        }

        [Test]
        public void GraphSerializer_Read_TwoRecordsWithOneId_TheSecondsLinksAreNotApplied()
        {
            BehaviorGraph written = GraphRig.OneState(new WaitTask(100f), "Idle");
            BehaviorGraphAsset asset = Written(written);
            GraphRecord root = asset.Find(asset.RootId);
            int brain = written.Brain.Id;
            asset.Records.Add(new GraphRecord
            {
                Id = root.Id, Type = "aikit.state", Data = root.Data,
                Links = new List<GraphLink> { new() { Field = nameof(AgentState.Children), Ids = new List<int> { brain } } }
            });
            var issues = new List<GraphIssue>();

            BehaviorGraph graph = GraphSerializer.Read(asset, issues);

            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.DanglingEdge && issue.Message.Contains("share id")));
            Assert.AreEqual(written.Root.Children.Count, graph.Root.Children.Count,
                "The first record of the id is the one read — its children, not the duplicate's");
        }

        [Test]
        public void BehaviorGraphAsset_Compile_AnUnreadableAsset_ItsErrorsLoggedOnce()
        {
            BehaviorGraphAsset asset = Written(GraphRig.OneState(new WaitTask(100f), "Idle"));
            asset.RootId = 99999;
            asset.MarkChanged();
            // The old root, which is not the root now, holds nodes of its own — and the read finds no root
            LogAssert.Expect(LogType.Error, new Regex("OldModel"));
            LogAssert.Expect(LogType.Error, new Regex("MissingRoot"));

            BehaviorGraph first = asset.Compile();
            BehaviorGraph second = asset.Compile();

            Assert.IsNull(first);
            Assert.IsNull(second);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BehaviorGraphAsset_Peek_AnUnreadableAsset_IsReadOnceUntilItChanges()
        {
            BehaviorGraphAsset asset = Written(GraphRig.OneState(new WaitTask(100f), "Idle"));
            int root = asset.RootId;
            asset.RootId = 99999;
            asset.MarkChanged();

            Assert.IsNull(asset.Peek());
            asset.RootId = root;                    // fixed behind the asset's back: not read again until it is changed
            Assert.IsNull(asset.Peek(), "Not read again on every repaint");

            asset.MarkChanged();
            Assert.IsNotNull(asset.Peek(), "Changed: read anew");
        }

        // ---------- upgrades ----------

        [Test]
        public void GraphDocument_OpeningAnOldGraph_TheUpgradeNotesStayInItsIssues()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            var charge = new ChargeTask();
            g.Chain(g.Decision(new ChargeReasoner(), 1), charge);
            BehaviorGraphAsset asset = Written(g.Build());
            asset.FormatVersion = 0;

            var document = new GraphDocument(asset);

            Assert.AreEqual(GraphUpgrades.Current, asset.FormatVersion, "Upgraded and saved");
            Assert.IsTrue(document.Issues.Exists(issue => issue.Code == GraphIssueCode.OldFormat && issue.RecordId == charge.Id &&
                                                          !issue.IsError),
                "The note of the upgrade is still shown after the save: " + string.Join("\n", document.Issues));
        }

        [Test]
        public void GraphDocument_OpeningAnOldGraphWithTakeCover_TheReplacedNodeNoteStays()
        {
            BehaviorGraphAsset asset = OldGraph(out int decision, out int block);
            Rewrite(asset, decision, TacticsNodesUpgrade.TAKE_COVER_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":2,\"NotFrom\":[]}");
            Rewrite(asset, block, TacticsNodesUpgrade.TAKE_COVER,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Target\":\"\",\"Kind\":\"Cover\"}");

            var document = new GraphDocument(asset);

            Assert.AreEqual(GraphUpgrades.Current, asset.FormatVersion, "Upgraded and saved");
            Assert.IsTrue(document.Issues.Exists(issue => issue.Code == GraphIssueCode.ReplacedNode &&
                                                          issue.Message.Contains("Behind is the Cover's own")),
                string.Join("\n", document.Issues));
        }

        [Test]
        public void BehaviorGraphAsset_Compile_AnUnsavedOldGraphWithPickUp_NoUnknownVariable()
        {
            BehaviorGraphAsset asset = OldGraph(out int decision, out int block);
            Rewrite(asset, decision, TacticsNodesUpgrade.FETCH_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":2,\"NotFrom\":[],\"Kind\":\"Rock\"}");
            Rewrite(asset, block, TacticsNodesUpgrade.PICK_UP,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Kind\":\"Rock\"}");
            Assert.IsFalse(asset.Variables.Exists(variable => variable.Name == "Item"), "Set-up: not declared in the asset");

            BehaviorGraph compiled = asset.Compile();         // an error it logged would fail the test (the warnings
                                                                // are the upgrade's notes)
            Assert.IsNotNull(compiled);
            Assert.IsFalse(asset.Issues.Any(issue => issue.Code == GraphIssueCode.UnknownVariable),
                string.Join("\n", asset.Issues));
            Assert.IsTrue(compiled.Variables.Exists(variable => variable.Name == "Item" &&
                                                                variable.Type == VariableType.Transform));
        }

        [Test]
        public void GraphUpgrades_AnOldGraphWithPickUpAndThrow_EveryNodeHasItsOwnId()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            var fetch = new DailyRoutineReasoner();
            var pickUp = new WaitTask(1f);
            var throwDecision = new DailyRoutineReasoner();
            var throwBlock = new WaitTask(1f);
            g.Do(g.Decision(fetch, 1), g.Block(pickUp));
            g.Do(g.Decision(throwDecision, 2), g.Block(throwBlock));
            BehaviorGraphAsset asset = Written(g.Build());
            asset.FormatVersion = 0;
            Rewrite(asset, fetch.Id, TacticsNodesUpgrade.FETCH_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":1,\"NotFrom\":[]}");
            Rewrite(asset, pickUp.Id, TacticsNodesUpgrade.PICK_UP,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Kind\":\"Rock\"}");
            Rewrite(asset, throwDecision.Id, ThrowUpgrade.THROW_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":2,\"NotFrom\":[]}");
            Rewrite(asset, throwBlock.Id, ThrowUpgrade.THROW_BLOCK,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Target\":\"\"}");
            int highest = asset.Records.Where(record => record.Type != GraphSerializer.TRANSITION).Max(record => record.Id);
            Assert.AreEqual(throwBlock.Id, highest, "Set-up: the Throw block's record has the highest node id");

            BehaviorGraph graph = GraphSerializer.Read(asset, new List<GraphIssue>());
            var ids = new List<int>();
            graph.VisitNodes(node => ids.Add(node.Id));

            Assert.IsFalse(ids.Contains(0), "Every node has an id");
            Assert.AreEqual(ids.Count, ids.Distinct().Count(),
                "No two nodes share an id: " + string.Join(",", ids.GroupBy(id => id).Where(group => group.Count() > 1)
                    .Select(group => group.Key)));
            var attacks = new List<AgentNode>();
            graph.VisitNodes(node =>
            {
                if (node is AttackTask)
                    attacks.Add(node);
            });
            Assert.AreEqual(1, attacks.Count);
            Assert.AreEqual(throwBlock.Id, attacks[0].Id, "The Attack keeps the old Throw block's id");
        }

        [Test]
        public void TacticsNodesUpgrade_TheNewNodes_StandNextToTheReplacedBlock_NeverAtZero()
        {
            BehaviorGraphAsset asset = OldGraph(out int decision, out int block);
            Rewrite(asset, decision, TacticsNodesUpgrade.FETCH_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":2,\"NotFrom\":[]}");
            Rewrite(asset, block, TacticsNodesUpgrade.PICK_UP,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Kind\":\"Rock\"}");
            foreach (GraphRecord record in asset.Records)
                record.Position += new Vector2(500f, 700f);

            var document = new GraphDocument(asset);
            var states = new List<AgentState>();
            document.Graph.CollectStates(states);
            AgentState holder = states.Single(state => state.Tasks.Exists(task => task is FindTask));
            AgentState walking = states.Single(state => state.Tasks.Exists(task => task is MoveToTask));
            AgentState interacting = states.Single(state => state.Tasks.Exists(task => task is InteractTask));
            Vector2 at = document.PositionOf(holder);
            Vector2 walk = document.PositionOf(walking);
            Vector2 interact = document.PositionOf(interacting);

            Assert.AreNotEqual(Vector2.zero, walk, "Move To is not at (0, 0)");
            Assert.AreNotEqual(Vector2.zero, interact, "Interact is not at (0, 0)");
            Assert.AreNotEqual(walk, interact, "Not on top of each other");
            Assert.Greater(walk.x, at.x, "Right of the node it follows");
            Assert.Greater(interact.x, walk.x);
            Assert.AreEqual(walk, asset.Find(walking.Id).Position, "Saved where it stands");
        }

        // ---------- the first layout ----------

        [Test]
        public void GraphSerializer_Layout_SiblingBlocks_GetPlacesOfTheirOwn()
        {
            BehaviorGraph graph = BehaviorPresets.MeleeChaser(new BehaviorPresets.Tuning { TokenPool = "Melee" });
            BehaviorGraphAsset asset = Written(graph);
            var states = new List<AgentState>();
            graph.CollectStates(states);
            AgentState circling = states.Single(state => state.Tasks.Exists(task => task is CircleTargetTask));
            AgentState asking = states.Single(state => state.Tasks.Exists(task => task is TakeAttackSlotTask));

            Assert.AreNotEqual(asset.Find(circling.Id).Position, asset.Find(asking.Id).Position,
                "Circle's Do and its On Tick lead to two blocks: each stands at a place of its own");
            var places = new HashSet<Vector2>();
            foreach (AgentState state in states.Where(state => state != graph.Root))
                Assert.IsTrue(places.Add(asset.Find(state.Id).Position), $"'{state.DisplayName}' stands on another node");
        }

        [Test]
        public void GraphSerializer_Layout_AFreshAsset_BrainDecisionsChainsSensorsAndStrays()
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            g.BrainTask.Target = TARGET;
            var detection = new DetectionTask(new RadiusSensorTask()) { StoreIn = TARGET };
            g.Sensor(detection);
            AgentState a = g.Decision(new WatchReasoner(), 1);
            AgentState[] aChain = g.Chain(a, new WaitTask(1f), new WaitTask(2f));
            AgentState b = g.Decision(new PatrolReasoner(), 2);
            AgentState[] bChain = g.Chain(b, new WaitTask(3f));
            AgentState stray = g.Block(new WaitTask(4f));
            BehaviorGraph graph = g.Build();

            BehaviorGraphAsset asset = Written(graph);

            Assert.AreEqual(Vector2.zero, Place(asset, graph.Brain));
            Assert.AreEqual(new Vector2(380f, 0f), Place(asset, a));
            Assert.AreEqual(new Vector2(760f, 0f), Place(asset, aChain[0]));
            Assert.AreEqual(new Vector2(1140f, 0f), Place(asset, aChain[1]));
            Assert.AreEqual(new Vector2(380f, 260f), Place(asset, b));
            Assert.AreEqual(new Vector2(760f, 260f), Place(asset, bChain[0]));
            Assert.AreEqual(new Vector2(380f, 520f), Place(asset, stray));
            Assert.AreEqual(new Vector2(-380f, 0f), asset.Find(detection.Id).Position);
            Assert.IsFalse(asset.Records.Exists(record => record.Type == GraphSerializer.TRANSITION && record.IsLink),
                "Every arrow leads forward: no links");
        }

        // ---------- the rig ----------

        private BehaviorGraphAsset Written(BehaviorGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            asset.name = "Graph";
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            return asset;
        }

        private static Vector2 Place(BehaviorGraphAsset asset, AgentState state) => asset.Find(state.Id).Position;

        // A graph of before step 90: a decision (to be rewritten) whose Do leads to a block (to be rewritten); the Brain
        // thinks about Target
        private BehaviorGraphAsset OldGraph(out int decision, out int block)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            var routine = new DailyRoutineReasoner();
            var wait = new WaitTask(1f);
            g.Do(g.Decision(routine, 2), g.Block(wait));
            BehaviorGraphAsset asset = Written(g.Build());
            asset.FormatVersion = 0;
            decision = routine.Id;
            block = wait.Id;
            return asset;
        }

        private static void Rewrite(BehaviorGraphAsset asset, int id, string type, string data)
        {
            GraphRecord record = asset.Find(id);
            record.Type = type;
            record.Data = data.Replace("\"Id\":0", $"\"Id\":{id}");
            asset.MarkChanged();
        }
    }
}
