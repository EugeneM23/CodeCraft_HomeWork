using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// A graph written and read (whole-plugin recheck, data): what a write keeps of the look (positions by id, the bends
    /// of two alike arrows), how it numbers the arrows and lists the assets, what it copies and stamps; what a read says
    /// of a missing asset, a link to the wrong kind, an arrow of a trigger that is gone; the validator's subgraph cycle,
    /// arrow checks and attack chains; the condition checks of attack places and of the wired object; the links of
    /// arrows that run back; Peek and Issues read once until the asset changes.
    /// </summary>
    public sealed class DataSerializerTests
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

        // ---------- the write ----------

        [Test]
        public void GraphSerializer_Write_KeepsThePositionsOfTheNodesThatStay_NoNewLayout()
        {
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState wait = g.Block(new WaitTask(1f), "Wait");
            g.Do(routine, wait);
            BehaviorGraph graph = g.Build();
            BehaviorGraphAsset asset = Written(graph);
            asset.Find(wait.Id).Position = new Vector2(123f, 456f);
            asset.Find(graph.Brain.Id).Position = new Vector2(-50f, 70f);
            Vector2 decisionAt = asset.Find(routine.Id).Position;

            AgentState added = g.Block(new WaitTask(2f), "Added");
            g.Then(wait, added);
            GraphSerializer.Write(graph, asset);

            Assert.AreEqual(new Vector2(123f, 456f), asset.Find(wait.Id).Position);
            Assert.AreEqual(new Vector2(-50f, 70f), asset.Find(graph.Brain.Id).Position);
            Assert.AreEqual(decisionAt, asset.Find(routine.Id).Position);
            Assert.AreEqual(Vector2.zero, asset.Find(added.Id).Position,
                "An asset that has a look is not laid out anew: a node added in code has no place of its own yet");
        }

        [Test]
        public void GraphSerializer_Write_TwoAlikeArrows_KeepTheirOwnBends()
        {
            var g = new BrainGraphBuilder();
            AgentState a = g.Block(new WaitTask(1f), "A");
            AgentState b = g.Block(new WaitTask(1f), "B");
            g.Do(g.Always(), a);
            g.Then(a, b);
            g.Then(a, b);
            BehaviorGraph graph = g.Build();
            BehaviorGraphAsset asset = Written(graph);
            GraphLinks.RecordOf(asset, a, 0).Waypoints = new List<Vector2> { new(1f, 1f) };
            GraphLinks.RecordOf(asset, a, 1).Waypoints = new List<Vector2> { new(2f, 2f) };

            g.Block(new WaitTask(3f), "Elsewhere");
            GraphSerializer.Write(graph, asset);

            CollectionAssert.AreEqual(new[] { new Vector2(1f, 1f) }, GraphLinks.RecordOf(asset, a, 0).Waypoints);
            CollectionAssert.AreEqual(new[] { new Vector2(2f, 2f) }, GraphLinks.RecordOf(asset, a, 1).Waypoints,
                "Two arrows alike: each keeps its own bends");
        }

        [Test]
        public void GraphSerializer_Write_ArrowIdsStartAThousandAboveTheHighestNodeId()
        {
            BehaviorGraph graph = GraphRig.OneState(new WaitTask(1f), "Idle");
            int highest = 0;
            graph.VisitNodes(node => highest = Mathf.Max(highest, node.Id));
            BehaviorGraphAsset asset = Written(graph);

            List<int> arrows = asset.Records.Where(record => record.Type == GraphSerializer.TRANSITION)
                .Select(record => record.Id).OrderBy(id => id).ToList();
            var nodes = new HashSet<int>(asset.Records.Where(record => record.Type != GraphSerializer.TRANSITION)
                .Select(record => record.Id));

            Assert.IsNotEmpty(arrows);
            CollectionAssert.AreEqual(Enumerable.Range(highest + 1000, arrows.Count), arrows);
            Assert.IsFalse(arrows.Any(nodes.Contains), "No arrow takes a node's id");
        }

        [Test]
        public void GraphSerializer_Write_TheSameAbilityTwice_OneEntryInObjects()
        {
            AbilityAsset grenade = Ability("Grenade");
            var g = new BrainGraphBuilder();
            var decision = new UseAbilityReasoner { Ability = grenade };
            g.Chain(g.Decision(decision, 1), new UseAbilityTask(grenade), new UseAbilityTask(grenade));
            BehaviorGraphAsset asset = Written(g.Build());

            Assert.AreEqual(1, asset.Objects.Count, "One asset, one entry");
            List<GraphLink> links = asset.Records.SelectMany(record => record.Links).Where(link => link.Field == "Ability").ToList();
            Assert.AreEqual(3, links.Count);
            Assert.IsTrue(links.All(link => link.Object == 0));

            BehaviorGraph read = GraphSerializer.Read(asset, new List<GraphIssue>());
            var abilities = new List<AbilityAsset>();
            read.VisitNodes(node =>
            {
                if (node is UseAbilityReasoner reasoner)
                    abilities.Add(reasoner.Ability);
                if (node is UseAbilityTask task)
                    abilities.Add(task.Ability);
            });
            Assert.AreEqual(3, abilities.Count);
            Assert.IsTrue(abilities.All(ability => ability == grenade), "Read back: the same asset");
        }

        [Test]
        public void GraphSerializer_Write_VariablesAreCopies_ReflexesCleared_TheFormatStamped()
        {
            var g = new BrainGraphBuilder();
            g.Variable(new VariableDeclaration("Pause", VariableType.Float) { Float = 0.25f });
            g.Do(g.Always(), g.Block(new WaitTask(1f)));
            BehaviorGraph graph = g.Build();
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            asset.Reflexes.Add(5);
            asset.FormatVersion = 0;

            GraphSerializer.Write(graph, asset);
            graph.Variables[0].Float = 9f;

            Assert.AreEqual(0.25f, asset.Variables[0].Float, "The asset holds a copy, not the graph's own declaration");
            Assert.IsEmpty(asset.Reflexes, "The old model's reflexes are never written");
            Assert.AreEqual(GraphUpgrades.Current, asset.FormatVersion);
            Assert.IsEmpty(asset.Objects, "No link names an asset");
        }

        // ---------- the read ----------

        [Test]
        public void GraphSerializer_Read_AnObjectLinkToAMissingAsset_IsNull_AndTheGraphSaysNoAbility()
        {
            AbilityAsset grenade = Ability("Grenade");
            var g = new BrainGraphBuilder();
            var decision = new UseAbilityReasoner { Ability = grenade };
            AgentState node = g.Decision(decision, 1);
            g.Chain(node, new UseAbilityTask());
            BehaviorGraphAsset asset = Written(g.Build());
            asset.Objects[0] = null;                           // the asset was deleted

            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);

            Assert.IsNull(((UseAbilityReasoner)read.Brain.Transitions[0].Target.Reasoner).Ability);
            Assert.IsFalse(issues.Exists(issue => issue.IsError), string.Join("\n", issues));
            List<GraphIssue> checks = GraphValidator.Validate(asset);
            Assert.IsTrue(checks.Exists(issue => issue.Code == GraphIssueCode.NoAbility && issue.RecordId == node.Id),
                string.Join("\n", checks));
        }

        [Test]
        public void GraphSerializer_Read_ALinkToTheWrongKind_IsDanglingEdgeNamingTheField()
        {
            AgentState idle = null;
            BehaviorGraph graph = GraphRig.OneState(new WaitTask(1f), "Idle");
            var states = new List<AgentState>();
            graph.CollectStates(states);
            idle = states.Single(state => state.Name == "Idle");
            BehaviorGraphAsset asset = Written(graph);
            int arrow = asset.Records.First(record => record.Type == GraphSerializer.TRANSITION).Id;
            asset.Find(idle.Id).Links.Single(link => link.Field == nameof(AgentState.Tasks)).Ids[0] = arrow;

            List<GraphIssue> issues = GraphValidator.Validate(asset);

            List<GraphIssue> dangling = issues.Where(issue => issue.Code == GraphIssueCode.DanglingEdge).ToList();
            Assert.AreEqual(1, dangling.Count, string.Join("\n", issues));
            StringAssert.Contains($"'Idle'.Tasks points to {arrow}, which is not a AgentTask", dangling[0].Message);
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.EmptyState && issue.RecordId == idle.Id),
                "The node is left with no block");
        }

        [Test]
        public void GraphSerializer_Read_AnArrowOfAnUnknownTrigger_IsTheOldModel_SomethingGone()
        {
            var wait = new WaitTask(1f);
            BehaviorGraph graph = GraphRig.OneState(wait, "Idle");
            var states = new List<AgentState>();
            graph.CollectStates(states);
            AgentState idle = states.Single(state => state.Name == "Idle");
            BehaviorGraphAsset asset = Written(graph);
            const int OLD = 99000;
            asset.Records.Add(new GraphRecord
            {
                Id = OLD, Type = GraphSerializer.TRANSITION,
                Data = JsonUtility.ToJson(new Transition { Trigger = (TransitionTrigger)7 })
            });
            GraphRecord node = asset.Find(idle.Id);
            GraphLink arrows = node.Links.Find(link => link.Field == nameof(AgentState.Transitions));
            if (arrows == null)
                node.Links.Add(arrows = new GraphLink { Field = nameof(AgentState.Transitions) });
            arrows.Ids.Add(OLD);

            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);

            List<GraphIssue> old = issues.Where(issue => issue.Code == GraphIssueCode.OldModel).ToList();
            Assert.AreEqual(1, old.Count, string.Join("\n", issues));
            Assert.AreEqual(OLD, old[0].RecordId);
            Assert.IsTrue(old[0].IsError);
            StringAssert.Contains("something gone", old[0].Message);
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.DanglingEdge), "Its node's link to it is not a dangling one");
            Assert.IsNotNull(read?.Brain, "The rest is read");
        }

        // ---------- the validator ----------

        [Test]
        public void GraphValidator_SubgraphCycle_AssetsLeadingBack_IsAnErrorNamingThePath()
        {
            BehaviorGraphAsset a = Written(GraphRig.OneState(new WaitTask(1f), "Idle"));
            a.name = "A";
            BehaviorGraphAsset b = Written(GraphRig.OneState(new WaitTask(1f), "Idle"));
            b.name = "B";
            a.Objects.Add(b);
            b.Objects.Add(a);

            List<GraphIssue> cycles = GraphValidator.Validate(a).Where(issue => issue.Code == GraphIssueCode.SubgraphCycle).ToList();

            Assert.AreEqual(1, cycles.Count);
            Assert.IsTrue(cycles[0].IsError);
            Assert.AreEqual(a.RootId, cycles[0].RecordId);
            StringAssert.Contains("A → B → A", cycles[0].Message);

            BehaviorGraphAsset c = Written(GraphRig.OneState(new WaitTask(1f), "Idle"));
            c.Objects.Add(Ability("Grenade"));
            Assert.IsFalse(GraphValidator.Validate(c).Exists(issue => issue.Code == GraphIssueCode.SubgraphCycle),
                "An ability is a leaf: no cycle");
        }

        [Test]
        public void GraphValidator_ABlockLeadingOnAtOnce_AndAnArrowIntoADecision_AreWarningsNamingBoth()
        {
            var g = new BrainGraphBuilder();
            AgentState routine = g.Always();
            AgentState first = g.Block(new WaitTask(1f), "First");
            g.Do(routine, first);
            first.AddTransition(new Transition { Target = g.Block(new WaitTask(1f), "Second"), Trigger = TransitionTrigger.Always });
            first.AddTransition(new Transition { Target = routine, Trigger = TransitionTrigger.TasksSucceeded });

            List<GraphIssue> issues = GraphValidator.Validate(Written(g.Build()))
                .Where(issue => issue.Code == GraphIssueCode.DanglingEdge && issue.RecordId == first.Id).ToList();

            Assert.AreEqual(2, issues.Count, string.Join("\n", issues));
            Assert.IsTrue(issues.All(issue => !issue.IsError));
            Assert.IsTrue(issues.Exists(issue => issue.Message.Contains("leads on at once to 'Second'")));
            Assert.IsTrue(issues.Exists(issue => issue.Message.Contains("only the Brain takes decisions")));
        }

        [Test]
        public void GraphValidator_ChainAttacks_StopsAtDecisions_FollowsLoopsOnce()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState d = g.Decision(new FightReasoner(), 1, "D");
            AgentState wait = g.Block(new WaitTask(1f), "Wait");
            AgentState branch = g.Block(new BranchTask(new CompareCondition("X", NumberComparison.Greater, 1f)), "Branch");
            g.Do(d, wait);
            g.Then(wait, branch);
            g.Then(branch, wait);                            // a loop back
            g.OnFail(branch, g.Block(new AttackTask(), "Strike"));
            AgentState e = g.Decision(new ChaseReasoner(), 2, "E");
            AgentState pause = g.Block(new WaitTask(1f), "Pause");
            g.Do(e, pause);
            g.Then(pause, d);                                // into a decision: the chain stops there
            AgentState r = g.Decision(new TargetAttacksReaction(), 3, "R");
            g.Do(r, g.Block(new WaitTask(0.2f), "Dodge"));
            g.Variable("X", VariableType.Float);

            Assert.IsTrue(GraphValidator.ChainAttacks(d), "Three blocks on, past a loop followed once");
            Assert.IsFalse(GraphValidator.ChainAttacks(e), "It stops at the decision its arrow leads into");

            List<GraphIssue> below = GraphValidator.Validate(Written(g.Build()))
                .Where(issue => issue.Code == GraphIssueCode.ReactionBelowDecision).ToList();
            Assert.AreEqual(1, below.Count);
            Assert.AreEqual(r.Id, below[0].RecordId);
            StringAssert.Contains("'D'", below[0].Message);
        }

        // ---------- the condition checks ----------

        [Test]
        public void ConditionChecks_CircleTargetWaitingForSlot_Asks_NotWaiting_DoesNot()
        {
            var asks = new List<GraphIssue>();
            ConditionChecks.Check(Waiting(new CircleTargetTask { WaitForSlot = true }, out _), asks);
            Assert.IsFalse(asks.Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot), string.Join("\n", asks));

            var silent = new List<GraphIssue>();
            ConditionChecks.Check(Waiting(new CircleTargetTask { WaitForSlot = false }, out AttackSlotCondition mine), silent);
            List<GraphIssue> nobody = silent.Where(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot).ToList();
            Assert.AreEqual(1, nobody.Count);
            Assert.AreEqual(mine.Id, nobody[0].RecordId);
            StringAssert.Contains("the common group", nobody[0].Message);
        }

        [Test]
        public void ConditionChecks_ANamedGroupIsLetPassByAnAttack_TheCommonOneIsNot()
        {
            var named = new List<GraphIssue>();
            ConditionChecks.Check(Attacking("Ranged"), named);
            Assert.IsFalse(named.Exists(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot),
                "A weapon's shot may ask its own group");

            var common = new List<GraphIssue>();
            ConditionChecks.Check(Attacking(""), common);
            Assert.AreEqual(1, common.Count(issue => issue.Code == GraphIssueCode.NobodyAsksForSlot));
        }

        [Test]
        public void ConditionChecks_AGetOfTheWiredObject_NothingWired_IsUnwiredValue_WiredItIsNot()
        {
            var get = new GetValue { Of = Whose.WiredObject, Object = "" };
            var unwired = new List<GraphIssue>();
            ConditionChecks.Check(Comparing(get), unwired);
            List<GraphIssue> found = unwired.Where(issue => issue.Code == GraphIssueCode.UnwiredValue).ToList();
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(get.Id, found[0].RecordId);
            StringAssert.Contains("reads the wired object", found[0].Message);

            var wired = new List<GraphIssue>();
            ConditionChecks.Check(Comparing(new GetValue { Of = Whose.WiredObject, Object = TARGET }), wired);
            Assert.IsFalse(wired.Exists(issue => issue.Code == GraphIssueCode.UnwiredValue));
        }

        // ---------- links ----------

        [Test]
        public void GraphLinks_LinkLoops_AFreeLabelBackIsDockedAgain_ATargetStraightBelowIsALink_AnOnTickBackIsALink()
        {
            var g = new BrainGraphBuilder();
            AgentState x = g.Always("X");
            AgentState a = g.Block(new WaitTask(1f), "A");
            AgentState b = g.Block(new WaitTask(1f), "B");
            AgentState c = g.Block(new WaitTask(1f), "C");
            AgentState d = g.Block(new WaitTask(1f), "D");
            AgentState t = g.Block(new SetNumberTask { Variable = "N", Value = 1f }, "T");
            g.Variable("N", VariableType.Float);
            g.Do(x, a);
            g.OnTick(x, t);
            g.Then(a, b);
            g.OnFail(a, c);
            g.Then(c, d);
            BehaviorGraph graph = g.Build();
            BehaviorGraphAsset asset = Written(graph);
            Place(asset, graph.Brain, 0f, 0f);
            Place(asset, x, 380f, 0f);
            Place(asset, a, 760f, 0f);
            Place(asset, b, 500f, 0f);                      // left of A
            Place(asset, c, 1140f, 0f);
            Place(asset, d, 1140f, 300f);                   // straight below C
            Place(asset, t, 100f, 200f);                    // left of X
            foreach (GraphRecord record in asset.Records.Where(record => record.Type == GraphSerializer.TRANSITION))
                record.IsLink = false;
            GraphRecord ab = GraphLinks.RecordOf(asset, a, 0);
            ab.IsLink = true;
            ab.LinkFree = true;
            ab.LinkPosition = new Vector2(5f, 5f);
            ab.Waypoints = new List<Vector2> { new(1f, 1f) };

            int made = GraphLinks.LinkLoops(asset, graph);

            Assert.AreEqual(3, made);
            Assert.IsFalse(ab.LinkFree, "A free label back to its pin");
            Assert.IsEmpty(ab.Waypoints);
            Assert.IsTrue(GraphLinks.RecordOf(asset, c, 0).IsLink, "Straight below: a link");
            Assert.IsTrue(GraphLinks.RecordOf(asset, x, x.Transitions.FindIndex(arrow => arrow.Trigger == TransitionTrigger.Tick)).IsLink,
                "An On Tick back: a link");
            Assert.IsFalse(GraphLinks.RecordOf(asset, x, 0).IsLink, "A wire forward stays a wire");
            for (int i = 0; i < graph.Brain.Transitions.Count; i++)
                Assert.IsFalse(GraphLinks.RecordOf(asset, graph.Brain, i).IsLink, "The Brain's wires are untouched");
        }

        // ---------- read once ----------

        [Test]
        public void BehaviorGraphAsset_Peek_ReadOnceUntilChanged_LogsNothing_TheCompiledOneFirst()
        {
            var g = new BrainGraphBuilder();
            g.Do(g.Always(), g.Block(new WaitTask(1f)));
            g.Block(new WaitTask(1f), "Stray");                     // a warning: no chain reaches it
            BehaviorGraphAsset asset = Written(g.Build());

            BehaviorGraph first = asset.Peek();
            Assert.AreSame(first, asset.Peek(), "Read once");
            IReadOnlyList<GraphIssue> issues = asset.Issues;
            Assert.AreSame(issues, asset.Issues, "The issues too");
            asset.MarkChanged();
            Assert.AreNotSame(first, asset.Peek(), "Changed: read anew");

            BehaviorGraph compiled = asset.Compile();
            Assert.AreSame(compiled, asset.Peek(), "Compiled: the compiled one");
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

        private AbilityAsset Ability(string name)
        {
            AbilityAsset ability = AbilityAsset.Create(name, new RangeCondition { Max = 5f }, new SelfDelivery());
            ability.hideFlags = HideFlags.HideAndDontSave;
            _created.Add(ability);
            return ability;
        }

        private static void Place(BehaviorGraphAsset asset, AgentState state, float x, float y) =>
            asset.Find(state.Id).Position = new Vector2(x, y);

        // The Attack waits for its place (an Attack Slot mine); Circle's chain is this block
        private static BehaviorGraph Waiting(AgentTask block, out AttackSlotCondition mine)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            mine = new AttackSlotCondition(AttackSlotIs.Mine);
            g.Do(g.Decision(new FightReasoner { Condition = mine }, 1), g.Block(new WaitTask(1f)));
            g.Do(g.Decision(new CircleReasoner(), 2), g.Block(block));
            return g.Build();
        }

        // An Attack (an Attack Slot mine of the group) whose chain strikes
        private static BehaviorGraph Attacking(string pool)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(new FightReasoner { Condition = new AttackSlotCondition(AttackSlotIs.Mine, pool) }, 1),
                g.Block(new AttackTask()));
            return g.Build();
        }

        // A Branch comparing a Get, in a chain
        private static BehaviorGraph Comparing(GetValue get)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState branch = g.Block(new BranchTask(new CompareCondition(get, NumberComparison.Greater, 1f)));
            g.Do(g.Decision(new WatchReasoner(), 1), branch);
            return g.Build();
        }
    }
}
