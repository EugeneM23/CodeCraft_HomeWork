using System;
using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The upgrade steps of step 90 (whole-plugin recheck, data; handbook §29а.3 — an explicit migration that says what it
    /// did): Attack Slot's own AND / OR / inverted AND, a Circle that asks already, a graph of the current format left
    /// alone; a step that fails is a warning and the others still run; the steps' order; the tactics nodes read into
    /// What → Find → Move To / Climb To → Interact with their numbers, variables and arrows; Throw's decision; where the
    /// nodes a step adds stand.
    /// </summary>
    public sealed class DataUpgradeTests
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

        // ---------- Attack Slot ----------

        [Test]
        public void AttackSlotUpgrade_AnInvertedAndOfItsOwn_IsWrappedInANewAnd_NotJoined()
        {
            var tired = new CompareCondition("Tired", VariableType.Bool);
            var notTired = new AllCondition(tired);
            var fight = new FightReasoner { Closer = 100f, Condition = notTired };
            BehaviorGraphAsset asset = OldGraph(fight);
            GraphRecord record = asset.Find(notTired.Id);
            record.Data = "{\"Invert\":true," + record.Data.Substring(1);    // an AND of before, under "not"

            BehaviorGraph graph = Read(asset, out _);
            ReasonerTask read = Decision(graph, 1);

            var both = read.Condition as AllCondition;
            Assert.IsNotNull(both, "A new AND joins the two");
            Assert.AreEqual(2, both.Conditions.Count);
            var kept = both.Conditions.OfType<AllCondition>().SingleOrDefault();
            Assert.IsNotNull(kept, "The inverted AND is kept whole");
            Assert.AreEqual(1, kept.Conditions.Count, "Nothing was put into it");
            Assert.IsFalse(((CompareCondition)kept.Conditions[0]).Expected,
                "Its \"not\" went into its only condition: \"Tired is off\"");
            var slot = both.Conditions.OfType<AttackSlotCondition>().Single();
            Assert.AreEqual(AttackSlotIs.Mine, slot.Is);

            // The meaning: (not tired) and (my place) — never "not (tired and my place)"
            var world = new AIWorld();
            var withWorld = new GraphRig(trace: false, world: world);
            Transform target = withWorld.CreateTargetAt(new Vector3(1f, 0f, 0f));
            withWorld.Context.Blackboard.Set("Tired", false);
            using (withWorld.Context.AimAt(target))
                Assert.AreEqual(Truth.No, read.Condition.Ask(withWorld.Context), "Not tired, but no place: no");
            var alone = new GraphRig(trace: false);
            alone.Context.Blackboard.Set("Tired", false);
            using (alone.Context.AimAt(target))
                Assert.AreEqual(Truth.Yes, read.Condition.Ask(alone.Context), "Not tired, nothing limits the attack: yes");
        }

        [Test]
        public void AttackSlotUpgrade_AnAndOfItsOwn_TheSlotJoinsThatAnd()
        {
            var rage = new CompareCondition("Rage", NumberComparison.Greater, 2f);
            var fight = new FightReasoner { Condition = new AllCondition(rage) };
            BehaviorGraphAsset asset = OldGraph(fight);

            ReasonerTask read = Decision(Read(asset, out _), 1);

            var and = read.Condition as AllCondition;
            Assert.IsNotNull(and);
            Assert.AreEqual(2, and.Conditions.Count, "The slot joined the AND it had: no AND around an AND");
            Assert.AreEqual("Rage", and.Conditions.OfType<CompareCondition>().Single().Variable);
            AttackSlotCondition slot = and.Conditions.OfType<AttackSlotCondition>().Single();
            Assert.AreEqual(AttackSlotIs.Mine, slot.Is);
            Assert.AreEqual("", slot.Pool);
        }

        [Test]
        public void AttackSlotUpgrade_ASlotInsideAnOr_IsLeftAlone()
        {
            var any = new AnyCondition(new AttackSlotCondition(AttackSlotIs.Mine),
                new CompareCondition("Rage", NumberComparison.Greater, 2f));
            var fight = new FightReasoner { Condition = any };
            BehaviorGraphAsset asset = OldGraph(fight);
            var issues = new List<GraphIssue>();
            var elements = new Dictionary<int, object>();
            BehaviorGraph graph = ReadWithoutUpgrades(asset, issues, elements);

            bool changed = new AttackSlotUpgrade().Apply(asset, graph, elements, issues);

            Assert.IsFalse(changed, "It has its Attack Slot already (inside an OR): nothing to do");
            var read = Decision(graph, 1).Condition as AnyCondition;
            Assert.IsNotNull(read);
            Assert.AreEqual(2, read.Conditions.Count);
        }

        [Test]
        public void AttackSlotUpgrade_TheJoiningAnd_StandsAtAndOffset_ItsSlotBesideIt()
        {
            var rage = new CompareCondition("Rage", NumberComparison.Greater, 2f);
            var fight = new FightReasoner { Condition = rage };
            BehaviorGraphAsset asset = OldGraph(fight);
            foreach (GraphRecord record in asset.Records)
                record.Position += new Vector2(500f, 700f);
            Vector2 compareAt = asset.Find(rage.Id).Position;
            Vector2 decisionAt = asset.Find(StateOf(asset, fight.Id)).Position;

            BehaviorGraph graph = Read(asset, out _);
            GraphSerializer.Write(graph, asset);

            var and = (AllCondition)Decision(graph, 1).Condition;
            AttackSlotCondition slot = and.Conditions.OfType<AttackSlotCondition>().Single();
            Assert.AreEqual(decisionAt + AttackSlotUpgrade.AND_OFFSET, asset.Find(and.Id).Position);
            Assert.AreEqual(decisionAt + AttackSlotUpgrade.AND_OFFSET + new Vector2(-200f, 60f), asset.Find(slot.Id).Position);
            Assert.AreEqual(compareAt, asset.Find(rage.Id).Position, "The old Compare keeps its own place");
        }

        [Test]
        public void AttackSlotUpgrade_ACircleThatAsksAlready_GetsNoSecondAsk()
        {
            var circle = new CircleReasoner();
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            AgentState waiting = g.Decision(circle, 1);
            g.Do(waiting, g.Block(new WaitTask(100f)));
            g.OnTick(waiting, g.Block(new TakeAttackSlotTask { WaitTime = 0f, Pool = "Melee" }));
            BehaviorGraphAsset asset = Written(g.Build());
            GraphRecord record = asset.Find(circle.Id);
            record.Data = record.Data.Substring(0, record.Data.Length - 1) + ",\"AskForPlace\":true,\"Pool\":\"Melee\"}";
            asset.FormatVersion = 0;
            int nodes = g.Root.Children.Count;

            BehaviorGraph graph = Read(asset, out _);
            AgentState read = graph.Brain.Transitions[0].Target;

            Assert.AreEqual(1, read.Transitions.Count(arrow => arrow.Trigger == TransitionTrigger.Tick), "It asks already");
            Assert.AreEqual(nodes, graph.Root.Children.Count, "No second ask was added");
            var slot = read.Reasoner.Condition as AttackSlotCondition;
            Assert.IsNotNull(slot);
            Assert.AreEqual(AttackSlotIs.NotMine, slot.Is);
            Assert.AreEqual("Melee", slot.Pool);
        }

        [Test]
        public void GraphUpgrades_ACurrentFormatGraph_ASlotlessAttack_StaysSlotless()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Do(g.Decision(new FightReasoner(), 1), g.Block(new WaitTask(1f)));
            AgentState circle = g.Decision(new CircleReasoner(), 2);
            g.Do(circle, g.Block(new WaitTask(1f)));
            g.Do(g.Decision(new ChaseReasoner(), 3), g.Block(new WaitTask(1f)));
            BehaviorGraphAsset asset = Written(g.Build());
            Assert.AreEqual(GraphUpgrades.Current, asset.FormatVersion, "Set-up: written now — the current format");
            int nodes = g.Root.Children.Count;

            BehaviorGraph graph = Read(asset, out _);

            Assert.IsFalse(asset.UpgradedOnRead, "No step runs on a graph of the current format");
            foreach (Transition wire in graph.Brain.Transitions)
                Assert.IsNull(wire.Target.Reasoner.Condition, $"'{wire.Target.DisplayName}' got no Attack Slot");
            Assert.IsFalse(graph.Brain.Transitions[1].Target.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.Tick),
                "Circle got no ask");
            Assert.AreEqual(nodes, graph.Root.Children.Count);
        }

        // ---------- the steps ----------

        [Test]
        public void GraphUpgrades_AFailingStep_IsAWarningNamingIt_TheOthersStillRun_TheDocumentIsReadOnly()
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            var charge = new ChargeTask();
            g.Chain(g.Decision(new ChargeReasoner(), 1), charge);
            BehaviorGraphAsset asset = Written(g.Build());
            asset.name = ThrowingStep.ASSET;
            asset.FormatVersion = 0;

            var document = new GraphDocument(asset);

            GraphIssue failed = document.Issues.FirstOrDefault(issue => issue.Type == nameof(ThrowingStep));
            Assert.AreEqual(GraphIssueCode.OldModel, failed.Code, string.Join("\n", document.Issues));
            Assert.IsFalse(failed.IsError, "A warning: the graph is read without what the step would have done");
            Assert.AreEqual(asset.RootId, failed.RecordId);
            StringAssert.Contains("failed: boom", failed.Message);
            Assert.IsTrue(document.Issues.Exists(issue => issue.Code == GraphIssueCode.OldFormat && issue.RecordId == charge.Id),
                "The other steps ran");
            Assert.IsTrue(document.IsReadOnly, "Half upgraded: shown, not edited");
            Assert.AreEqual(0, asset.FormatVersion, "Not saved");
        }

        [Test]
        public void GraphUpgrades_Steps_InVersionThenNameOrder_CurrentIsTheHighest()
        {
            List<Type> order = GraphUpgrades.Steps.Select(step => step.GetType()).ToList();
            Type[] module =
            {
                typeof(AttackSlotUpgrade), typeof(ChargeUpgrade), typeof(TacticsNodesUpgrade), typeof(ThrowUpgrade),
                typeof(NotUpgrade), typeof(DecisionPinsUpgrade)
            };

            int last = -1;
            foreach (Type step in module)
            {
                int at = order.IndexOf(step);
                Assert.Greater(at, last, $"{step.Name} in the order of version, then name");
                last = at;
            }

            Assert.IsTrue(GraphUpgrades.Steps
                .Where(step => module.Contains(step.GetType()) && step is not NotUpgrade and not DecisionPinsUpgrade)
                .All(step => step.Version == 1));
            Assert.AreEqual(2, new NotUpgrade().Version, "\"not\" removed (15.11.2026): after the step-90 steps");
            Assert.AreEqual(GraphUpgrades.Steps.Max(step => step.Version), GraphUpgrades.Current);
            Assert.AreEqual(3, new DecisionPinsUpgrade().Version, "the decisions' own pins removed (15.11.2026): after it");
            Assert.AreEqual(3, GraphUpgrades.Current);
        }

        // ---------- the tactics nodes ----------

        [Test]
        public void TacticsNodesUpgrade_UseTurret_FindTurretUse_MoveTo_InteractUse_ItsVariableAndThreatKept()
        {
            BehaviorGraph graph = OldTactics(TacticsNodesUpgrade.USE_TURRET,
                "{\"Turret\":\"Gun\",\"Target\":\"Enemy\",\"Pace\":{\"_useVariable\":false,\"_scope\":0,\"_name\":\"\",\"_constant\":0.6}}",
                out AgentState holder);

            var find = (FindTask)holder.Tasks.Single();
            Assert.AreEqual("Gun", find.StoreIn);
            Assert.AreEqual(typeof(Turret).FullName, find.What.Component);
            Assert.AreEqual(Offer.Use, find.What.Offer);
            Assert.AreEqual(0f, find.What.Radius);
            var move = (MoveToTask)Next(holder).Tasks.Single();
            Assert.AreEqual("Gun", move.Target);
            Assert.AreEqual(0.6f, move.Pace.Get(null), 1e-4f);
            var interact = (InteractTask)Next(Next(holder)).Tasks.Single();
            Assert.AreEqual("Gun", interact.Object);
            Assert.AreEqual(Offer.Use, interact.Interaction);
            Assert.AreEqual("Enemy", interact.Target, "Its threat kept");
            Assert.IsTrue(graph.Variables.Exists(variable => variable.Name == "Gun" && variable.Type == VariableType.Transform));
        }

        [Test]
        public void TacticsNodesUpgrade_LoadTurret_OfferLoad_NoThreat_DefaultVariableTurret()
        {
            OldTactics(TacticsNodesUpgrade.LOAD_TURRET, "{}", out AgentState holder);

            var find = (FindTask)holder.Tasks.Single();
            Assert.AreEqual("Turret", find.StoreIn, "No variable named: Turret");
            Assert.AreEqual(Offer.Load, find.What.Offer);
            Assert.AreEqual(0f, find.What.Radius);
            var move = (MoveToTask)Next(holder).Tasks.Single();
            Assert.AreEqual(1f, move.Pace.Get(null), 1e-4f, "No pace: a full one");
            var interact = (InteractTask)Next(Next(holder)).Tasks.Single();
            Assert.AreEqual(Offer.Load, interact.Interaction);
            Assert.AreEqual("", interact.Target);
        }

        [Test]
        public void TacticsNodesUpgrade_HighGroundBlock_ClimbsTo_ThePerchBandDefaults()
        {
            OldTactics(TacticsNodesUpgrade.HIGH_GROUND, "{\"Target\":\"\"}", out AgentState holder);

            WhatValue what = ((FindTask)holder.Tasks.Single()).What;
            Assert.AreEqual(typeof(Perch).FullName, what.Component);
            Assert.AreEqual(Offer.Occupy, what.Offer);
            Assert.AreEqual(6f, what.Radius);
            Assert.IsTrue(what.FromTarget);
            Assert.AreEqual(3f, what.MinDistance);
            Assert.AreEqual(14f, what.MaxDistance);
            var climb = Next(holder).Tasks.Single() as ClimbToTask;
            Assert.IsNotNull(climb, "To a perch it climbs");
            Assert.AreEqual("Perch", climb.Target);
            Assert.AreEqual(Offer.Occupy, ((InteractTask)Next(Next(holder)).Tasks.Single()).Interaction);
        }

        [Test]
        public void TacticsNodesUpgrade_TheDecisions_TheirNeedAsAnInvertedIs_PriorityNotFromAndEveryKept()
        {
            (string Type, string Extra, CharacterState? Need, float Pause)[] cases =
            {
                (TacticsNodesUpgrade.TAKE_COVER_DECISION, "", CharacterState.InCover, new SearchForReasoner().Rest),
                (TacticsNodesUpgrade.USE_TURRET_DECISION, "", CharacterState.Occupying, new SearchForReasoner().Rest),
                (TacticsNodesUpgrade.HIGH_GROUND_DECISION, ",\"Every\":8.0", CharacterState.Occupying, 8f),
                (TacticsNodesUpgrade.RELOAD_TURRET_DECISION, "", null, new SearchForReasoner().Rest)
            };

            foreach ((string type, string extra, CharacterState? need, float rest) in cases)
            {
                BehaviorGraphAsset asset = OldGraph(out int decision, out _, out _, out _);
                Rewrite(asset, decision, type,
                    $"{{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":3,\"NotFrom\":[77]{extra}}}");

                var search = Read(asset, out _).Brain.Transitions[0].Target.Reasoner as SearchForReasoner;

                Assert.IsNotNull(search, $"{type}: Search For");
                Assert.AreEqual(3, search.Priority, type);
                CollectionAssert.AreEqual(new[] { 77 }, search.NotFrom, type);
                Assert.AreEqual(rest, search.Rest, 1e-4f, type);
                if (need == null)
                {
                    Assert.IsNull(search.Condition, type);
                    continue;
                }

                var condition = search.Condition as IsCondition;
                Assert.IsNotNull(condition, type);
                Assert.AreEqual(Whose.Me, condition.Of, type);
                Assert.AreEqual(need.Value, condition.State, type);
                Assert.AreEqual(IsCheck.IsNot, condition.Is, type);
            }
        }

        [Test]
        public void TacticsNodesUpgrade_AWiredSearchRadiusFallsBack_AWiredPaceIsKept()
        {
            const string WIRED = "{\"_useVariable\":true,\"_scope\":0,\"_name\":\"R\",\"_constant\":0.0}";
            OldTactics(TacticsNodesUpgrade.PICK_UP,
                $"{{\"Kind\":\"Rock\",\"SearchRadius\":{WIRED},\"Pace\":{{\"_useVariable\":true,\"_scope\":0,\"_name\":\"P\",\"_constant\":0.0}}}}",
                out AgentState pickUp);
            OldTactics(TacticsNodesUpgrade.TAKE_COVER, $"{{\"SearchRadius\":{WIRED}}}", out AgentState cover);

            Assert.AreEqual(30f, ((FindTask)pickUp.Tasks.Single()).What.Radius, "A wired radius: the default one");
            WhatValue coverWhat = ((FindTask)cover.Tasks.Single()).What;
            Assert.AreEqual(15f, coverWhat.Radius);
            Assert.AreEqual(5f, coverWhat.MinDistance, "Absent: the band's default");
            Assert.AreEqual(18f, coverWhat.MaxDistance);
            var move = (MoveToTask)Next(pickUp).Tasks.Single();
            Assert.IsTrue(move.Pace.UsesVariable, "A wired pace stays wired");
            Assert.AreEqual("P", move.Pace.Name);
        }

        [Test]
        public void TacticsNodesUpgrade_AnOldEitherWayArrow_LeavesFromInteract_AFailedOneStaysOnFind()
        {
            BehaviorGraphAsset asset = OldGraph(out int decision, out int block, out AgentTask either, out AgentTask failed,
                eitherWay: true);
            Rewrite(asset, decision, TacticsNodesUpgrade.FETCH_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":2,\"NotFrom\":[]}");
            Rewrite(asset, block, TacticsNodesUpgrade.PICK_UP, "{\"Kind\":\"Rock\"}");

            BehaviorGraph graph = Read(asset, out _);
            AgentState find = graph.Brain.Transitions[0].Target.Transitions[0].Target;
            AgentState move = Next(find);
            AgentState interact = Next(move);

            Assert.IsTrue(find.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.TasksSucceeded && arrow.Target == move));
            Assert.IsTrue(find.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.TasksFailed &&
                                                           arrow.Target.Tasks[0].Id == failed.Id), "Nothing Found: as before");
            Assert.IsFalse(find.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.TasksFinished));
            Assert.IsTrue(interact.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.TasksFinished &&
                                                               arrow.Target.Tasks[0].Id == either.Id),
                "Its either-way arrow: from the end of the new chain");
            Assert.IsTrue(move.Transitions.Exists(arrow => arrow.Trigger == TransitionTrigger.TasksSucceeded && arrow.Target == interact));
        }

        [Test]
        public void TacticsNodesUpgrade_AHolderWithASecondBlock_IsLeftUnread_TheGraphOpensReadOnly()
        {
            BehaviorGraphAsset asset = OldGraph(out _, out int block, out _, out AgentTask failed);
            GraphRecord holder = asset.Records.Single(record => record.Type == "aikit.state" &&
                                                               record.Links.Exists(link => link.Field == nameof(AgentState.Tasks) &&
                                                                                           link.Ids.Contains(block)));
            Rewrite(asset, failed.Id, TacticsNodesUpgrade.PICK_UP, "{\"Kind\":\"Rock\"}");
            holder.Links.Single(link => link.Field == nameof(AgentState.Tasks)).Ids.Add(failed.Id);   // a second block

            var issues = new List<GraphIssue>();
            GraphSerializer.Read(asset, issues);

            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.UnknownNodeType && issue.RecordId == failed.Id),
                "Not a node's one block: left as read");
            Assert.IsFalse(issues.Exists(issue => issue.Code == GraphIssueCode.ReplacedNode && issue.Type == TacticsNodesUpgrade.PICK_UP));
            asset.MarkChanged();
            Assert.IsTrue(new GraphDocument(asset).IsReadOnly);
        }

        [Test]
        public void TacticsNodesUpgrade_AVariableOfThatNameExists_ItIsReused_NotDeclaredTwice()
        {
            BehaviorGraphAsset asset = OldGraph(out int decision, out int block, out _, out _);
            asset.Variables.Add(new VariableDeclaration("Item", VariableType.Transform));
            Rewrite(asset, decision, TacticsNodesUpgrade.FETCH_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":2,\"NotFrom\":[]}");
            Rewrite(asset, block, TacticsNodesUpgrade.PICK_UP, "{\"Kind\":\"Rock\"}");
            int before = asset.Variables.Count;

            BehaviorGraph graph = Read(asset, out _);

            Assert.AreEqual(1, graph.Variables.Count(variable => variable.Name == "Item"));
            Assert.AreEqual(before, graph.Variables.Count);
        }

        [Test]
        public void ThrowUpgrade_TheDecision_ClosesFromTheWeapon_GetsNoSlot_BesideAnUpgradedAttack()
        {
            var fight = new FightReasoner();
            var placeholder = new DailyRoutineReasoner();
            BehaviorGraphAsset asset = OldGraph(fight, placeholder);
            Rewrite(asset, placeholder.Id, ThrowUpgrade.THROW_DECISION,
                "{\"Name\":\"\",\"Id\":0,\"IsBackground\":false,\"Priority\":2,\"NotFrom\":[],\"Closer\":5,\"LetGoAfter\":6}");

            BehaviorGraph graph = Read(asset, out _);

            var thrown = Decision(graph, 2) as FightReasoner;
            Assert.IsNotNull(thrown, "Throw is the Attack decision");
            Assert.AreEqual(0f, thrown.Closer, "Within the reach of the weapon in the hand");
            Assert.AreEqual(0f, thrown.LetGoAfter);
            Assert.IsNull(thrown.Condition, "The Throw decision asked for no place");
            StringAssert.Contains("taken within", thrown.Requirement);
            Assert.IsInstanceOf<AttackSlotCondition>(Decision(graph, 1).Condition, "The old Attack got its Attack Slot");
        }

        // ---------- where the added nodes stand ----------

        [Test]
        public void UpgradePlaces_ANearNodeWithoutARecord_NoPlace_AChainResolvesToTheRecordedOne()
        {
            var g = new BrainGraphBuilder();
            AgentState z = g.Block(new WaitTask(1f), "Z");
            BehaviorGraphAsset asset = Written(g.Build());
            asset.Find(z.Id).Position = new Vector2(100f, 100f);
            var y = new AgentState("Y") { Id = 9999 };
            var x = new AgentState("X") { Id = 9998 };
            var never = new AgentState("Never") { Id = 9997 };

            UpgradePlaces.PlaceNear(x, y, new Vector2(10f, 0f));
            Assert.IsFalse(UpgradePlaces.TryPlaceOf(asset, x, out _), "Y has no record and no place of its own");

            UpgradePlaces.PlaceNear(y, z, new Vector2(0f, 20f));
            Assert.IsTrue(UpgradePlaces.TryPlaceOf(asset, x, out Vector2 at));
            Assert.AreEqual(new Vector2(110f, 120f), at, "Through Y to Z's record: the offsets add up");
            Assert.IsFalse(UpgradePlaces.TryPlaceOf(asset, never, out _), "A node no step placed");
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

        // An old graph (format 0) of these decisions, each in priority order with a Wait behind its Do; no old keys
        private BehaviorGraphAsset OldGraph(params ReasonerTask[] decisions)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            int priority = 1;
            foreach (ReasonerTask decision in decisions)
                g.Do(g.Decision(decision, priority++), g.Block(new WaitTask(100f)));
            BehaviorGraphAsset asset = Written(g.Build());
            asset.FormatVersion = 0;
            return asset;
        }

        // An old graph: a decision whose Do leads to a block (both to be rewritten); the block's Done (or with eitherWay —
        // its either-way end) leads to one Debug Log, its Failed to another
        private BehaviorGraphAsset OldGraph(out int decision, out int block, out AgentTask after, out AgentTask failed,
            bool eitherWay = false)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            var routine = new DailyRoutineReasoner();
            var wait = new WaitTask(1f);
            after = new DebugLogTask();
            failed = new DebugLogTask();
            AgentState waiting = g.Block(wait);
            g.Do(g.Decision(routine, 2), waiting);
            if (eitherWay)
                g.OnEnd(waiting, g.Block(after));
            else
                g.Then(waiting, g.Block(after));
            g.OnFail(waiting, g.Block(failed));
            BehaviorGraphAsset asset = Written(g.Build());
            asset.FormatVersion = 0;
            decision = routine.Id;
            block = wait.Id;
            return asset;
        }

        // An old graph whose block is rewritten to a removed tactics block of this data: read; the node that held it
        private BehaviorGraph OldTactics(string type, string data, out AgentState holder)
        {
            BehaviorGraphAsset asset = OldGraph(out _, out int block, out _, out _);
            Rewrite(asset, block, type, data);
            BehaviorGraph graph = Read(asset, out _);
            holder = graph.Brain.Transitions[0].Target.Transitions[0].Target;
            return graph;
        }

        private static void Rewrite(BehaviorGraphAsset asset, int id, string type, string data)
        {
            GraphRecord record = asset.Find(id);
            record.Type = type;
            record.Data = data.Replace("\"Id\":0", $"\"Id\":{id}");
            asset.MarkChanged();
        }

        private static BehaviorGraph Read(BehaviorGraphAsset asset, out List<GraphIssue> issues)
        {
            issues = new List<GraphIssue>();
            BehaviorGraph graph = GraphSerializer.Read(asset, issues);
            Assert.IsNotNull(graph, string.Join("\n", issues));
            return graph;
        }

        // The asset read as the format it says it is, with no step run (the step is then run by the test)
        private static BehaviorGraph ReadWithoutUpgrades(BehaviorGraphAsset asset, List<GraphIssue> issues,
            Dictionary<int, object> elements)
        {
            int format = asset.FormatVersion;
            asset.FormatVersion = GraphUpgrades.Current;
            BehaviorGraph graph = GraphSerializer.Read(asset, issues);
            asset.FormatVersion = format;
            graph.VisitNodes(node => elements[node.Id] = node);
            var states = new List<AgentState>();
            graph.CollectStates(states);
            foreach (AgentState state in states)
            {
                foreach (Transition arrow in state.Transitions)
                    elements[-1 - elements.Count] = arrow;
            }

            return graph;
        }

        // The decision of the given priority
        private static ReasonerTask Decision(BehaviorGraph graph, int priority) =>
            graph.Brain.Transitions.Select(wire => wire.Target.Reasoner).Single(reasoner => reasoner.Priority == priority);

        private static int StateOf(BehaviorGraphAsset asset, int taskId) =>
            asset.Records.Single(record => record.Type == "aikit.state" &&
                                           record.Links.Exists(link => link.Field == nameof(AgentState.Tasks) &&
                                                                       link.Ids.Contains(taskId))).Id;

        // The node a block's Done leads to
        private static AgentState Next(AgentState node) =>
            node.Transitions.First(arrow => arrow.Trigger == TransitionTrigger.TasksSucceeded).Target;

        /// <summary>
        /// An upgrade step of the test assembly (the steps are found in every assembly): it fails on the asset of the
        /// test that makes it fail, and does nothing on any other.
        /// </summary>
        private sealed class ThrowingStep : IGraphUpgrade
        {
            public const string ASSET = "Upgrade Throws";

            public int Version => 1;

            public bool Apply(BehaviorGraphAsset asset, BehaviorGraph graph, IReadOnlyDictionary<int, object> elements,
                List<GraphIssue> issues)
            {
                if (asset != null && asset.name == ASSET)
                    throw new InvalidOperationException("boom");
                return false;
            }
        }
    }
}
