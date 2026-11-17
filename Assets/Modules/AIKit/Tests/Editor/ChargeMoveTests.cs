using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The charge is a move (step 90, C14): its numbers and actions are the weapon's Rush, as its dashes are — the Charge
    /// node goes by them, and by its own only with a weapon that has none (a graph of before keeps working, and is told
    /// once); the Charge decision's reach 0 is the weapon's rush, and with a weapon that does not charge it is not taken.
    /// </summary>
    public sealed class ChargeMoveTests
    {
        private const string TARGET = "Target";
        private const float DT = 0.02f;

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

        private GraphRig Rig(WeaponAsset weapon, Vector3 target)
        {
            var rig = new GraphRig(trace: false);
            if (weapon != null)
                rig.GiveWeapon(weapon);
            Transform victim = rig.CreateTargetAt(target);
            rig.Context.Blackboard.Set(TARGET, victim);
            return rig;
        }

        private static WeaponAsset Charger(float chargeTime, float distance, float speed, float recovery)
        {
            WeaponAsset weapon = WeaponAsset.Create(new WeaponMove { Name = "Attack 1" });
            weapon.Charges = true;
            weapon.Rush = new WeaponRush
            {
                ChargeTime = chargeTime, AimSpeed = 720f, Distance = distance, Speed = speed, Recovery = recovery,
                ChargeAction = "Gore Up", RushAction = "Gore"
            };
            return weapon;
        }

        // Steps the task by hand until it is done (or the time is out); how long it took
        private static float Run(GraphRig rig, AgentTask task, float limit, out TaskStatus status)
        {
            status = TaskStatus.Running;
            float time = 0f;
            while (status == TaskStatus.Running && time < limit)
            {
                rig.Context.Advance(DT);
                status = task.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
                time += DT;
            }

            return time;
        }

        [Test]
        public void Charge_TheWeaponsRush_ItsNumbersAndActions_NotTheNodes()
        {
            const float GATHER = 0.4f, DISTANCE = 3f, SPEED = 10f, RECOVERY = 0.3f;
            GraphRig rig = Rig(Charger(GATHER, DISTANCE, SPEED, RECOVERY), new Vector3(0f, 0f, 20f));
            List<string> shown = new ActionLog(rig.Context) { AlwaysShowing = true }.Played;
            var charge = new ChargeTask { Target = TARGET, ChargeTime = 5f, Distance = 30f, Speed = 1f, Recovery = 5f };
            Vector3 from = rig.Actuator.Position;

            float took = Run(rig, charge, 10f, out TaskStatus status);

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.AreEqual(GATHER + DISTANCE / SPEED + RECOVERY, took, 3f * DT, "The weapon's times, not the node's");
            Assert.AreEqual(DISTANCE, Vector3.Distance(from, rig.Actuator.Position), 0.5f, "As far as the weapon rushes");
            CollectionAssert.AreEqual(new[] { "Gore Up", "Gore" }, shown, "The weapon's actions");
        }

        [Test]
        public void Charge_AWeaponWithNoRush_TheNodesOwnNumbers()
        {
            GraphRig rig = Rig(WeaponAsset.Create(new WeaponMove { Name = "Attack 1" }), new Vector3(0f, 0f, 20f));
            var charge = new ChargeTask { Target = TARGET, ChargeTime = 0.2f, Distance = 2f, Speed = 10f, Recovery = 0.2f };

            float took = Run(rig, charge, 10f, out TaskStatus status);

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.AreEqual(0.2f + 2f / 10f + 0.2f, took, 3f * DT, "A graph of before keeps working by its own numbers");
            StringAssert.Contains("weapon's Rush", charge.Requirement, "The node says whose numbers it goes by");
        }

        [Test]
        public void ChargeDecision_Within0_IsTheWeaponsRush_NoRush_NotTaken()
        {
            const float RUSH = 6f;
            var decision = new ChargeReasoner { Farther = 2f, Within = 0f };
            GraphRig rig = Rig(Charger(1f, RUSH, 10f, 1f), new Vector3(0f, 0f, RUSH - 1f));
            Transform target = rig.Context.Target;

            Assert.IsTrue(decision.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), false), out _));
            rig.MoveTargetTo(new Vector3(0f, 0f, RUSH + 1f));
            Assert.IsFalse(decision.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), false),
                out string far));
            StringAssert.Contains($"needs {RUSH:0.#} or nearer", far);

            rig.GiveWeapon(WeaponAsset.Create(new WeaponMove { Name = "Attack 1" }));
            Assert.IsFalse(decision.Holds(new ReasonerQuery(rig.Context, TargetFacts.Of(rig.Context, target), false),
                out string why));
            Assert.AreEqual("my weapon does not charge", why);
            Assert.AreEqual("within: the weapon's rush", decision.Requirement);
        }

        [Test]
        public void Charge_AGraphOfBefore_IsToldOnce_ThatTheNumbersAreTheWeapons()
        {
            var g = new BrainGraphBuilder();
            var charge = new ChargeTask();
            g.Chain(g.Decision(new ChargeReasoner()), charge);
            var asset = ScriptableObject.CreateInstance<BehaviorGraphAsset>();
            _created.Add(asset);
            GraphSerializer.Write(g.Build(), asset);
            asset.FormatVersion = 0;

            var issues = new List<GraphIssue>();
            GraphSerializer.Read(asset, issues);

            Assert.IsTrue(issues.Exists(issue => issue.Code == GraphIssueCode.OldFormat && issue.RecordId == charge.Id &&
                                                 !issue.IsError));
            Assert.IsTrue(asset.UpgradedOnRead, "Saved with the new format: told once");
            asset.FormatVersion = GraphUpgrades.Current;
            var again = new List<GraphIssue>();
            GraphSerializer.Read(asset, again);
            Assert.IsFalse(again.Exists(issue => issue.Code == GraphIssueCode.OldFormat));
        }

    }
}
