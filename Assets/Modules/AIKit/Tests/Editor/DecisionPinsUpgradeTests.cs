using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Every decision node has the same build (the user's word, 15.11.2026): Use Ability lost its Timer pin, Sleep its Night,
    /// Gather its Called — what they waited for is a condition in the Condition pin. A graph of before reads with the wire
    /// it had turned into a Compare there (DecisionPinsUpgrade), joined with a condition the decision had.
    /// </summary>
    public sealed class DecisionPinsUpgradeTests
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
        }

        [Test]
        public void AnOldGraph_TimerNightCalled_BecomeACompareInTheCondition_JoinedWithWhatItHad()
        {
            var g = new BrainGraphBuilder();
            g.Variable("Grenade Timer", VariableType.Float);
            g.Variable("Night", VariableType.Transform);
            g.Variable("Called", VariableType.Bool);
            var grenade = new UseAbilityReasoner { Condition = new AttackSlotCondition(AttackSlotIs.Mine) };
            var sleep = new SleepReasoner();
            var gather = new GatherDecision();
            g.Do(g.Decision(grenade, 1), g.Block(new WaitTask(1f)));
            g.Do(g.Decision(sleep, 2), g.Block(new WaitTask(1f)));
            g.Do(g.Decision(gather, 3), g.Block(new WaitTask(1f)));
            BehaviorGraphAsset asset = Written(g.Build());
            Old(asset, grenade.Id, "\"Timer\":{\"_useVariable\":true,\"_scope\":0,\"_name\":\"Grenade Timer\",\"_constant\":0.0},");
            Old(asset, sleep.Id, "\"Night\":\"Night\",");
            Old(asset, gather.Id, "\"Called\":{\"_useVariable\":true,\"_scope\":0,\"_name\":\"Called\",\"_constant\":false},");

            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);

            var readGrenade = (UseAbilityReasoner)Decision(read, grenade.Id);
            var both = readGrenade.Condition as AllCondition;
            Assert.IsNotNull(both, "Its Attack Slot and the timer, joined by an AND");
            CompareCondition timer = both.Conditions.OfType<CompareCondition>().Single();
            Assert.AreEqual("Grenade Timer", timer.Variable);
            Assert.AreEqual(NumberComparison.LessOrEqual, timer.Comparison);
            Assert.AreEqual(0f, timer.Value);
            Assert.IsTrue(issues.Exists(issue => issue.RecordId == grenade.Id && issue.Message.Contains("own")),
                "The graph is told the ability's own pause is asked too: " + string.Join("\n", issues));

            var night = (CompareCondition)Decision(read, sleep.Id).Condition;
            Assert.AreEqual("Night", night.Variable);
            Assert.AreEqual(VariableType.Transform, night.Kind);
            Assert.IsTrue(night.Expected, "Night is there");

            var called = (CompareCondition)Decision(read, gather.Id).Condition;
            Assert.AreEqual("Called", called.Variable);
            Assert.AreEqual(VariableType.Bool, called.Kind);
            Assert.IsTrue(called.Expected, "Called is on");
        }

        [Test]
        public void Decisions_HaveNoPinsOfTheirOwn_SleepAndGatherHoldWithNoCondition()
        {
            Assert.IsNull(typeof(UseAbilityReasoner).GetField("Timer"));
            Assert.IsNull(typeof(SleepReasoner).GetField("Night"));
            Assert.IsNull(typeof(GatherDecision).GetField("Called"));

            var rig = new GraphRig(trace: false);
            try
            {
                Assert.IsTrue(new SleepReasoner().HoldsWithCondition(new ReasonerQuery(rig.Context, new TargetFacts(null, 0f), false),
                    out _));
            }
            finally
            {
                GraphRig.DestroyAll();
            }
        }

        // ---------- inside ----------

        private BehaviorGraphAsset Written(BehaviorGraph graph)
        {
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(graph, asset);
            asset.FormatVersion = 2;
            return asset;
        }

        // The record as an older graph wrote it: its own pin's key in its data
        private static void Old(BehaviorGraphAsset asset, int id, string key)
        {
            GraphRecord record = asset.Find(id);
            record.Data = "{" + key + record.Data.Substring(1);
        }

        private static ReasonerTask Decision(BehaviorGraph graph, int id) =>
            graph.Root.Children.Select(state => state.Reasoner).First(decision => decision != null && decision.Id == id);
    }
}
