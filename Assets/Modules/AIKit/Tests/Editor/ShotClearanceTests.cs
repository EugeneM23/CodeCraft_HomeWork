using System.Collections.Generic;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A shooter does not shoot into the edge of a wall: Kite stops to shoot only with Clearance of room on each side of
    /// its line to the target (behind a corner it steps out wider than a bare line, which the angle of Min View Angle
    /// left centimetres from); Strike with a weapon that shoots neither starts nor lets go a shot whose own line — from the
    /// hands, with Shot.Clearance of room — runs into a wall (the body's launcher says); the arrow leaves toward the
    /// target whichever way the clip holds the body.
    /// </summary>
    public sealed class ShotClearanceTests
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

        [Test]
        public void Kite_ALineThatGrazesACorner_IsNoPlaceToShootFrom_ItStepsOutTillItHasClearance()
        {
            // A wall just in front of the archer and to its left: the bare line to the target passes its corner 0.25 m off
            var sight = new WallsSight().Wall(-3f, 0.8f, 0f, 1.2f);
            GraphRig rig = NewRig(new Vector3(0.25f, 0f, 0f), sight);
            Transform target = Target(rig, new Vector3(0.25f, 0f, 8f));
            Assert.That(sight.Room(rig.Actuator.Position, target.position), Is.EqualTo(0.25f).Within(0.01f), "Set-up");

            Assert.AreEqual(TaskStatus.Success, new KiteTask { Target = TARGET, Clearance = 0f }.Tick(rig.Context, DT),
                "With no Clearance the bare line (and the fan of Min View Angle) is enough: it would shoot past the edge");

            var kite = new KiteTask { Target = TARGET, MinDistance = 5f, MaxDistance = 9f };
            float clearance = kite.Clearance.Get(rig.Context);
            Assert.AreEqual(TaskStatus.Running, kite.Tick(rig.Context, DT), "Not from here");
            Assert.AreEqual(TaskStatus.Success, Run(rig, kite, 5f), "In Range — once it has stepped out");
            // The line the arrow takes — from in front of the body (half a metre ahead) to the target: a corner left just
            // behind the shoulder is not in its way
            Vector3 front = rig.Actuator.Position + (target.position - rig.Actuator.Position).normalized * 0.5f;
            Assert.GreaterOrEqual(sight.Room(front, target.position), clearance - 0.1f,
                "The line to the target has about Clearance of room from the wall, all the way ahead");
        }

        [Test]
        public void Strike_AShotThatWouldHitAWall_IsNotStarted_NorLetGo_TheMoveEndsOutOfReach()
        {
            GraphRig rig = NewRig(Vector3.zero, null);
            Transform target = Target(rig, new Vector3(0f, 0f, 8f));
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            var launcher = new FakeLauncher();
            rig.Context.Launcher = launcher;
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Shot", Reach = 16f, WindUp = 0.3f, Recovery = 0.3f });
            bow.Shoots = true;
            bow.Shot.Pool = "";
            rig.GiveWeapon(bow);

            launcher.Flies = false;
            Assert.AreEqual(TaskStatus.Failure, new AttackTask { Target = TARGET }.Tick(rig.Context, DT),
                "Its flight would hit a wall: no shot started (Out Of Reach — step out)");

            launcher.Flies = true;
            var shot = new AttackTask { Target = TARGET };
            Assert.AreEqual(TaskStatus.Success, Run(rig, shot, 1f), "A free line: the shot");
            Assert.AreEqual(1, launcher.Launches);

            // The target steps behind the wall while the bow is drawn: the arrow is held, the move ends Out Of Reach
            var held = new AttackTask { Target = TARGET };
            float drawn = rig.Context.LocalTime;
            TaskStatus answer = Run(rig, held, 1f, () => launcher.Flies = rig.Context.LocalTime - drawn < 0.1f);
            Assert.AreEqual(TaskStatus.Failure, answer);
            Assert.AreEqual(1, launcher.Launches, "No arrow into the wall");
            Assert.AreEqual(0, hits.Hits, "The fake launcher lands nothing itself");
        }

        [Test]
        public void ArrowLauncher_TheArrowsOwnLine_NeedsShotClearanceOfRoom_AndLeavesTowardTheTarget()
        {
            var shooter = new GameObject("Shooter") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(shooter);
            shooter.transform.rotation = Quaternion.LookRotation(Vector3.right);       // the clip holds it side-on
            var target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(target);
            target.transform.position = new Vector3(0f, 0f, 10f);
            var shot = new WeaponShot { Height = 1.4f, Forward = 0.5f, AimHeight = 1.2f, Clearance = 0.3f };

            var grazing = new WallsSight().Wall(-3f, 4f, -0.2f, 5f);                  // 0.2 m off the arrow's line
            var roomy = new WallsSight().Wall(-3f, 4f, -0.4f, 5f);                    // 0.4 m off
            var launch = new ProjectileLaunch(shooter.transform, target.transform, 1f, shot, grazing);

            ArrowLauncher.Line(launch, out Vector3 start, out Vector3 aim);
            Assert.That(Vector3.Distance(start, new Vector3(0f, 1.4f, 0.5f)), Is.LessThan(1e-4f),
                "It leaves in front of the body toward the target, not along the side-on body");
            Assert.That(Vector3.Distance(aim, new Vector3(0f, 1.2f, 10f)), Is.LessThan(1e-4f));
            Assert.IsTrue(grazing.HasLineOfSight(start, aim), "Set-up: the bare line is free");
            Assert.IsFalse(ArrowLauncher.Shared.CanFly(launch), "Only 0.2 m of room: no shot along it");
            Assert.IsTrue(ArrowLauncher.Shared.CanFly(new ProjectileLaunch(shooter.transform, target.transform, 1f, shot, roomy)),
                "0.4 m of room: it flies");
        }

        // ---------- the rig ----------

        private sealed class FakeLauncher : IProjectileLauncher
        {
            public bool Flies = true;
            public int Launches;

            public void Launch(in ProjectileLaunch launch) => Launches++;

            public bool CanFly(in ProjectileLaunch launch) => Flies;
        }

        private static GraphRig NewRig(Vector3 at, ISightProbe sight)
        {
            var rig = new GraphRig(trace: false);
            rig.Actuator.Position = at;
            rig.Context.Sight = sight;
            return rig;
        }

        private Transform Target(GraphRig rig, Vector3 at)
        {
            var go = new GameObject(TARGET) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            rig.Agent.Blackboard.Set(TARGET, go.transform);
            return go.transform;
        }

        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds, System.Action eachTick = null)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                eachTick?.Invoke();
                answer = task.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            return answer;
        }
    }
}
