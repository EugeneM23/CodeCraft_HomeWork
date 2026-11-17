using System.Collections.Generic;
using System.Linq;
using AIKit.Editor;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The ready set of reactions added to a graph the author drew (step 90, C8 — the lab's menu item for the skeleton
    /// graph runs it): nothing is rebuilt, the reactions go above the fight with the order of the author's decisions
    /// kept, their chains are those of handbook §19.2, Detection gets the Hit By shape, the new nodes land below the
    /// others, and a second run adds nothing.
    /// </summary>
    public sealed class ReactionTemplateTests
    {
        private const string TARGET = "Target";

        private BehaviorGraphAsset _asset;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            var g = new BrainGraphBuilder();
            g.Sensor(new DetectionTask(new RadiusSensorTask { Radius = 8f }) { Tag = "Player", StoreIn = TARGET });
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Chain(g.Decision(new NoticeReasoner(), 0), new WaitTask(1f));
            g.Chain(g.Decision(new ClimbReasoner(), 1), new WaitTask(1f));
            g.Chain(g.Decision(new FightReasoner(), 2), new AttackTask());
            g.Chain(g.Decision(new ChaseReasoner(), 3), new ChaseTask());
            g.Chain(g.Decision(new PatrolReasoner(), 4), new WaitTask(1f));
            BehaviorGraph graph = g.Build();
            GraphSerializer.Write(graph, _asset);
            float y = 0f;
            foreach (GraphRecord record in _asset.Records)
                record.Position = new Vector2(100f, y += 150f);     // the author's layout: down to y = 150 × records
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_asset);

        [Test]
        public void AddTo_TheReactionsTakeThePlaceOfTheFight_TheOthersKeepTheirOrder()
        {
            var document = new GraphDocument(_asset);

            ReactionTemplate.Added added = ReactionTemplate.AddTo(document);

            Dictionary<string, int> priorities = Priorities(new GraphDocument(_asset));
            Assert.AreEqual(0, priorities[Title<NoticeReasoner>()], "Above the fight: untouched");
            Assert.AreEqual(1, priorities[Title<ClimbReasoner>()]);
            Assert.AreEqual(2, priorities[Title<TargetAttacksReaction>()], "In the place the fight had");
            Assert.AreEqual(3, priorities[Title<GotHitReaction>()]);
            Assert.AreEqual(4, priorities[Title<FightReasoner>()], "The fight and the rest move down, in their order");
            Assert.AreEqual(5, priorities[Title<ChaseReasoner>()]);
            Assert.AreEqual(6, priorities[Title<PatrolReasoner>()]);
            Assert.IsTrue(((GotHitReaction)added.GotHit.Reasoner).FromBehindOnly, "A blow from the front was seen coming");
        }

        [Test]
        public void AddTo_TheChains_RandomToADashAsideOrBack_EitherBlockedToBlock_GotHitToTurnToTarget()
        {
            ReactionTemplate.AddTo(new GraphDocument(_asset));

            var document = new GraphDocument(_asset);
            List<AgentState> states = document.States();
            AgentState attacks = states.Single(state => state.Reasoner is TargetAttacksReaction);
            AgentState random = Single(attacks, TransitionTrigger.Always);
            Assert.IsNotNull(random.Random, "Do → Random");
            AgentState aside = random.Transitions.Single(arrow => arrow.Output == 0).Target;
            AgentState back = random.Transitions.Single(arrow => arrow.Output == 1).Target;
            Assert.AreEqual(DashDirection.Aside, ((DashTask)aside.Tasks[0]).Direction);
            Assert.AreEqual(DashDirection.Back, ((DashTask)back.Tasks[0]).Direction, "Aside or back — never toward the attacker");
            Assert.Greater(random.Random.Outputs[0].Weight, random.Random.Outputs[1].Weight, "Aside more often (§19.2)");
            AgentState block = Single(aside, TransitionTrigger.TasksFailed);
            Assert.IsInstanceOf<BlockTask>(block.Tasks[0], "A dash that cannot be made: block");
            Assert.AreSame(block, Single(back, TransitionTrigger.TasksFailed));

            AgentState gotHit = states.Single(state => state.Reasoner is GotHitReaction);
            Assert.IsInstanceOf<TurnToTargetTask>(Single(gotHit, TransitionTrigger.Always).Tasks[0]);
            AgentState brain = document.Graph.Brain;
            Assert.IsTrue(brain.Transitions.Exists(wire => wire.Target == attacks), "Wired to the Brain");
            Assert.IsTrue(brain.Transitions.Exists(wire => wire.Target == gotHit));
        }

        [Test]
        public void AddTo_DetectionGetsTheHitByShape()
        {
            ReactionTemplate.AddTo(new GraphDocument(_asset));

            DetectionTask detection = new GraphDocument(_asset).Detection();
            Assert.AreEqual(1, detection.Sensors.Count(shape => shape is HitBySensorTask), "Whoever struck is seen");
            Assert.AreEqual(1, detection.Sensors.Count(shape => shape is RadiusSensorTask), "The author's shape stays");
        }

        [Test]
        public void AddTo_TheGraphHasNoNewProblem_AndNoReactionBelowTheFight()
        {
            List<GraphCode> before = Codes();

            ReactionTemplate.AddTo(new GraphDocument(_asset));

            List<GraphCode> after = Codes();
            CollectionAssert.DoesNotContain(after.Select(code => code.Code), GraphIssueCode.ReactionBelowDecision);
            CollectionAssert.AreEquivalent(before.Where(code => code.IsError), after.Where(code => code.IsError),
                "No new error");
        }

        [Test]
        public void AddTo_TheNewNodesLandBelowTheAuthors()
        {
            float lowest = _asset.Records.Max(record => record.Position.y);
            var document = new GraphDocument(_asset);

            ReactionTemplate.Added added = ReactionTemplate.AddTo(document);

            foreach (AgentState state in new[] { added.TargetAttacks, added.Random, added.DashAside, added.DashBack, added.Block,
                         added.GotHit, added.Turn })
                Assert.Greater(document.PositionOf(state).y, lowest, $"{state.DisplayName} lands below the author's nodes");
        }

        [Test]
        public void AddTo_ASecondTime_AddsNothing()
        {
            ReactionTemplate.AddTo(new GraphDocument(_asset));
            int records = _asset.Records.Count;

            ReactionTemplate.Added again = ReactionTemplate.AddTo(new GraphDocument(_asset));

            Assert.IsFalse(again.Any, "What is there is kept and not added again");
            Assert.AreEqual(records, _asset.Records.Count);
        }

        // ---------- helpers ----------

        private readonly struct GraphCode
        {
            public readonly GraphIssueCode Code;
            public readonly bool IsError;
            public readonly string Message;

            public GraphCode(GraphIssue issue)
            {
                Code = issue.Code;
                IsError = issue.IsError;
                Message = issue.Message;
            }
        }

        private List<GraphCode> Codes() => GraphValidator.Validate(_asset).Select(issue => new GraphCode(issue)).ToList();

        private static string Title<T>() => NodeTitles.Of(typeof(T));

        private static Dictionary<string, int> Priorities(GraphDocument document) =>
            document.Graph.Brain.Transitions.Where(wire => wire.Target?.Reasoner != null)
                .ToDictionary(wire => wire.Target.DisplayName, wire => wire.Target.Reasoner.Priority);

        private static AgentState Single(AgentState from, TransitionTrigger trigger) =>
            from.Transitions.Single(arrow => arrow.Trigger == trigger).Target;
    }
}
