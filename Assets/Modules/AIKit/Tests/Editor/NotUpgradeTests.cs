using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// "not" is gone from conditions (the user's word, 15.11.2026): an Is says Is Not itself, and a condition of an older
    /// graph that had "not" says the same thing by its own setting once read (NotUpgrade) — or is named as a problem when
    /// it has none.
    /// </summary>
    public sealed class NotUpgradeTests
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

        [Test]
        public void Is_IsNot_SwapsYesAndNo_NoDataStaysNoData_AndSaysSo()
        {
            var rig = new GraphRig(trace: false);
            var go = new GameObject("Foe") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            GetTests.Status status = go.AddComponent<GetTests.Status>();
            rig.Context.Blackboard.Set(TARGET, go.transform);
            var blocking = new IsCondition(CharacterState.Blocking, TARGET) { Is = IsCheck.IsNot };

            Assert.AreEqual(Truth.NoData, blocking.Ask(rig.Context), "It does not say: no data, not \"yes\"");
            status.Say(CharacterState.Blocking, true);
            Assert.AreEqual(Truth.No, new IsCondition(CharacterState.Blocking, TARGET) { Is = IsCheck.IsNot }.Ask(rig.Context));
            status.Say(CharacterState.Blocking, false);
            Assert.AreEqual(Truth.Yes, new IsCondition(CharacterState.Blocking, TARGET) { Is = IsCheck.IsNot }.Ask(rig.Context));
            Assert.AreEqual("Target is not blocking", blocking.Phrase(rig.Context));
        }

        [Test]
        public void Turn_EachConditionSaysTheOppositeByItsOwnSetting()
        {
            var isArmed = new IsCondition { Of = Whose.Me, State = CharacterState.Armed };
            Assert.IsTrue(NotUpgrade.Turn(isArmed));
            Assert.AreEqual(IsCheck.IsNot, isArmed.Is);

            var flag = new CompareCondition("Alarm", VariableType.Bool);
            Assert.IsTrue(NotUpgrade.Turn(flag));
            Assert.IsFalse(flag.Expected, "Alarm is off");

            var less = new CompareCondition("Ammo", NumberComparison.Less, 3f);
            Assert.IsTrue(NotUpgrade.Turn(less));
            Assert.AreEqual(NumberComparison.GreaterOrEqual, less.Comparison, "not (< 3) is ≥ 3");

            var mine = new AttackSlotCondition(AttackSlotIs.Mine);
            Assert.IsTrue(NotUpgrade.Turn(mine));
            Assert.AreEqual(AttackSlotIs.NotMine, mine.Is);

            var single = new AllCondition(new CompareCondition("Tired", VariableType.Bool));
            Assert.IsTrue(NotUpgrade.Turn(single), "An AND of one: the not goes into it");
            Assert.IsFalse(((CompareCondition)single.Conditions[0]).Expected);

            Assert.IsFalse(NotUpgrade.Turn(new AnyCondition(new CompareCondition(), new CompareCondition())),
                "An OR of several has no opposite of its own");
            Assert.IsFalse(NotUpgrade.Turn(new AttackSlotCondition(AttackSlotIs.MineAndIAttackFirst)));
        }

        [Test]
        public void AnOldGraph_AnIsUnderNot_ReadsAsIsNot_AnOrUnderNot_IsAProblem()
        {
            var g = new BrainGraphBuilder();
            g.Variable(TARGET, VariableType.Transform);
            var armed = new IsCondition { Of = Whose.Me, State = CharacterState.Armed };
            var either = new AnyCondition(new CompareCondition("A", VariableType.Bool), new CompareCondition("B", VariableType.Bool));
            AgentState always = g.Always();
            AgentState first = g.Block(new BranchTask(armed));
            AgentState second = g.Block(new BranchTask(either));
            g.Do(always, first);
            g.Then(first, second);
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(g.Build(), asset);
            asset.FormatVersion = 1;
            foreach (int id in new[] { armed.Id, either.Id })
            {
                GraphRecord record = asset.Find(id);
                record.Data = "{\"Invert\":true," + record.Data.Substring(1);
            }

            var issues = new List<GraphIssue>();
            BehaviorGraph read = GraphSerializer.Read(asset, issues);

            IsCondition readIs = null;
            read.VisitNodes(node => readIs ??= node as IsCondition);
            Assert.AreEqual(IsCheck.IsNot, readIs.Is, "\"not Armed\" is Is Not Armed now");
            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.OldFormat && issue.RecordId == either.Id &&
                                                 issue.Message.Contains("not")), string.Join("\n", issues));
        }
    }
}
