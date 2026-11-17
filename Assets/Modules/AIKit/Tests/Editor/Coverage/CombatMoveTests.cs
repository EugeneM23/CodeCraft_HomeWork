using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The moves of the body and the weapon's data, at their corners (the step 90 recheck of the fight): a charge turns no
    /// faster than its aim speed while it gathers, rushes short of a wall ahead and holds the agent from its first step to
    /// the end of its stand; a guard held for no time is down after its first step; a switch of weapons holds the body
    /// against every other mover; the weapon's helpers answer for no moves and no dashes; the combat code scans no world.
    /// </summary>
    public sealed class CombatMoveTests
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

        private WeaponAsset Charger(WeaponRush rush)
        {
            WeaponAsset weapon = WeaponAsset.Create(new WeaponMove { Name = "Attack 1" });
            weapon.Charges = true;
            weapon.Rush = rush;
            _created.Add(weapon);
            return weapon;
        }

        private static GraphRig Rig(WeaponAsset weapon, Vector3 target)
        {
            var rig = new GraphRig(trace: false);
            rig.GiveWeapon(weapon);
            Transform victim = rig.CreateTargetAt(target);
            rig.Context.Blackboard.Set(TARGET, victim);
            return rig;
        }

        // One step of the charge by hand: the agent's clock, the task, the body
        private static TaskStatus Step(GraphRig rig, AgentTask task)
        {
            rig.Context.Advance(DT);
            TaskStatus status = task.Tick(rig.Context, DT);
            rig.Actuator.Apply(DT);
            return status;
        }

        // ---------- the charge ----------

        [Test]
        public void Charge_WeaponRush_TargetAtTheSide_AimTurnsAtMostAimSpeedTimesChargeTime()
        {
            const float GATHER = 0.5f, AIM_SPEED = 90f;
            GraphRig rig = Rig(Charger(new WeaponRush
            {
                ChargeTime = GATHER, AimSpeed = AIM_SPEED, Distance = 3f, Speed = 10f, Recovery = 0.2f
            }), new Vector3(10f, 0f, 0f));
            Assume.That(rig.Agent.transform.forward, Is.EqualTo(Vector3.forward), "It looks along +Z, the target is at +X");
            var charge = new ChargeTask { Target = TARGET };

            for (int i = 0; i < 100 && charge.PhaseOf(rig.Context) != ChargeTask.Phase.Rushing; i++)
                Step(rig, charge);

            Assert.AreEqual(ChargeTask.Phase.Rushing, charge.PhaseOf(rig.Context), "Set-up: it rushes");
            float turned = Vector3.Angle(Vector3.forward, charge.AimOf(rig.Context));
            Assert.AreEqual(AIM_SPEED * GATHER, turned, 2f,
                "While it gathered it turned no faster than its aim speed: the target at the side is followed late");
        }

        [Test]
        public void Charge_WeaponRush_WallAhead_RushesFreeDistanceLessWallGap()
        {
            const float FREE = 5f, WALL_GAP = 0.3f, SPEED = 14f;
            GraphRig rig = Rig(Charger(new WeaponRush
            {
                ChargeTime = 0.1f, AimSpeed = 720f, Distance = 12f, Speed = SPEED, Recovery = 0.2f
            }), new Vector3(0f, 0f, 20f));
            rig.Context.Walk = new CombatWallAhead(FREE);
            var charge = new ChargeTask { Target = TARGET };
            Vector3 from = rig.Actuator.Position;

            float rushing = 0f;
            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 500 && status == TaskStatus.Running; i++)
            {
                status = Step(rig, charge);
                if (charge.PhaseOf(rig.Context) == ChargeTask.Phase.Rushing)
                    rushing += DT;
            }

            float rushed = FREE - WALL_GAP;
            Assert.AreEqual(TaskStatus.Success, status);
            Assert.AreEqual(1, rig.Actuator.Dashes, "One rush, by the body's dash");
            Assert.AreEqual(rushed, Vector3.Distance(from, rig.Actuator.Position), 0.02f, "Short of the wall by the gap");
            Assert.AreEqual(rushed / SPEED, rushing, 2f * DT + 1e-3f, "At its speed: the shorter rush is quicker");
        }

        [Test]
        public void Charge_CommitsTheAgent_FromFirstStepToEndOfRecovery()
        {
            GraphRig rig = Rig(Charger(new WeaponRush
            {
                ChargeTime = 0.2f, AimSpeed = 720f, Distance = 2f, Speed = 10f, Recovery = 0.2f
            }), new Vector3(0f, 0f, 20f));
            var charge = new ChargeTask { Target = TARGET };
            var phases = new HashSet<ChargeTask.Phase>();

            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 200 && status == TaskStatus.Running; i++)
            {
                status = Step(rig, charge);
                if (status != TaskStatus.Running)
                    break;

                phases.Add(charge.PhaseOf(rig.Context));
                Assert.IsTrue(charge.CommitsFor(rig.Context), $"Committed while {charge.PhaseOf(rig.Context)}");
            }

            Assert.AreEqual(TaskStatus.Success, status);
            CollectionAssert.AreEquivalent(new[] { ChargeTask.Phase.Gathering, ChargeTask.Phase.Rushing, ChargeTask.Phase.Recovering },
                phases, "Set-up: every phase was seen");
            Assert.IsFalse(charge.CommitsFor(rig.Context), "Done: the Brain is free to choose again");
        }

        // ---------- the guard ----------

        [Test]
        public void Block_HoldZero_DoneOnItsFirstStep_GuardDownAfter()
        {
            var rig = new GraphRig(trace: false);
            var block = new BlockTask { Hold = 0f };

            Assert.AreEqual(TaskStatus.Success, block.Tick(rig.Context, DT), "No time to hold it: done at once");
            Assert.IsFalse(rig.Context.IsBlocking, "…and the guard is down");
        }

        // ---------- the switch of weapons ----------

        [Test]
        public void SwitchWeapon_DuringTheSwitch_AnotherMoverCannotMoveTheBody()
        {
            var rig = new GraphRig(trace: false);
            WeaponAsset sword = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 2f });
            sword.name = "Sword";
            sword.SheatheTime = 0.6f;
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Attack 1", Reach = 14f });
            bow.name = "Bow";
            rig.GiveWeapon(sword);
            rig.Carry(sword);
            rig.Carry(bow);
            var switching = new SwitchWeaponTask { Weapon = "Bow" };
            var other = new object();

            for (int i = 0; i < 5; i++)
                Assert.AreEqual(TaskStatus.Running, switching.Tick(rig.Context, DT), "Set-up: the switch goes on");

            Assert.IsFalse(rig.Context.Arbiter.TryClaim(other, BodyAxis.Move, MoveAuthority.Strafe),
                "While the switch goes on the body is its (frozen): nobody else moves it");

            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < 200 && status == TaskStatus.Running; i++)
                status = switching.Tick(rig.Context, DT);

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.AreSame(bow, rig.Context.Weapon, "Set-up: the bow is in the hand");
            Assert.IsTrue(rig.Context.Arbiter.TryClaim(other, BodyAxis.Move, MoveAuthority.Strafe), "Done: the body is free");
        }

        // ---------- the weapon's data ----------

        [Test]
        public void WeaponAsset_ShortestReachFindDashToRushOrNone()
        {
            WeaponAsset weapon = WeaponAsset.Create(new WeaponMove { Name = "Jab", Reach = 0f },
                new WeaponMove { Name = "Attack 1", Reach = 3f }, new WeaponMove { Name = "Heavy Swing", Reach = 5f });
            WeaponAsset none = WeaponAsset.Create();
            _created.Add(weapon);
            _created.Add(none);

            Assert.AreEqual(3f, weapon.ShortestReach, "A move of no reach is skipped");
            Assert.AreEqual(0f, none.ShortestReach, "No moves: 0");
            Assert.AreEqual("Jab", weapon.Find("").Name, "No name: the first move");
            Assert.IsNull(none.Find(""), "No moves: none");
            Assert.IsNull(weapon.Find("Heavy"), "No move of that name");
            Assert.IsNull(weapon.DashTo(DashDirection.Left), "No dashes");
            Assert.IsNull(weapon.RushOrNone, "It does not charge");
            weapon.Charges = true;
            Assert.AreSame(weapon.Rush, weapon.RushOrNone, "It charges by its Rush");
        }

        // ---------- invariant 7 ----------

        [Test]
        public void Invariant7_TheCombatCode_ScansNoWorld()
        {
            string asmdef = AssetDatabase.FindAssets("AIKit t:AssemblyDefinitionAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path => Path.GetFileName(path) == "AIKit.asmdef");
            Assert.IsNotNull(asmdef, "AIKit.asmdef");
            string core = Path.GetFullPath(FileUtil.GetPhysicalPath(Path.GetDirectoryName(asmdef)));
            string combat = Path.Combine(core, "Combat");
            Assert.IsTrue(Directory.Exists(combat), $"The combat code is where it is read from: {combat}");

            string[] patterns = { "FindObject", "FindAnyObject", "FindFirstObject", "OverlapCircle", "OverlapSphere",
                "OverlapBox", "Physics.", "Physics2D.", "GameObject.Find" };
            var found = new List<string>();
            int files = 0;
            foreach (string file in Directory.GetFiles(combat, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string text = lines[i].Trim();
                    if (!text.StartsWith("//") && patterns.Any(text.Contains))
                        found.Add($"{Path.GetFileName(file)}:{i + 1}: {text}");
                }
            }

            Assert.Greater(files, 10, "Set-up: the combat files were read");
            CollectionAssert.IsEmpty(found, "The fight reaches the world through the context, the roster, the finder");
        }
    }
}
