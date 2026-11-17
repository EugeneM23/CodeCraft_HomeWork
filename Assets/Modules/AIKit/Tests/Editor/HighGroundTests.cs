using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The high ground of a shooter as the chain of step 90 (the High Ground blocks are gone): Search For (on need: it
    /// stands on nothing) → Find (What: a perch to occupy, its top at shooting distance from the target, near by) →
    /// Climb To (the perch's top: its place) → Interact (Occupy — facing the target). Up there the Attack decision shoots
    /// from it; the target walked off, the Climb decision takes the shooter down. A fake climbing body does the climbs.
    /// </summary>
    public sealed class HighGroundTests
    {
        private const string TARGET = "Target";
        private const string PERCH = "Perch";
        private const float DT = 0.02f;
        private const float STEP = 0.05f;

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
        public void PerchChain_APerchNearBy_ClimbsOntoIt_OccupiesItFacingTheTarget()
        {
            GraphRig rig = NewRig(out FakeClimber climber, out Perch perch, new Vector3(4f, 0f, 0f));
            Transform target = Target(rig, new Vector3(14f, 0f, 0f));
            var find = new FindTask { What = Perches(), StoreIn = PERCH };
            var climb = new ClimbToTask { Target = PERCH };
            var occupy = new InteractTask { Object = PERCH, Interaction = Offer.Occupy, Target = TARGET };

            Assert.AreEqual(TaskStatus.Success, find.Tick(rig.Context, DT), "Found");
            Assert.AreSame(perch.transform, rig.Context.Blackboard.Get<Transform>(PERCH));
            Assert.AreEqual(TaskStatus.Success, Run(rig, climb, 8f), "Climbed: on top");
            Assert.AreEqual(1, climber.Climbs, "One climb up");
            Assert.IsTrue(perch.IsOn(rig.Actuator.Position), "To its top — the perch's place — not its foot");
            Assert.IsFalse(climber.ClimbAllowed, "The walls are shut again");
            Assert.AreEqual(TaskStatus.Success, Run(rig, occupy, 1f), "Occupied");
            Assert.AreSame(perch.transform, rig.Context.Occupied);
            Assert.Less(Vector3.Angle(rig.Actuator.Facing, Flat(target.position - rig.Actuator.Position)), 1f,
                "Facing the target");
        }

        [Test]
        public void PerchChain_NoPerchNearBy_OrItsTopOutOfShootingDistance_NothingFound()
        {
            GraphRig far = NewRig(out _, out _, new Vector3(20f, 0f, 0f));
            Target(far, new Vector3(-10f, 0f, 0f));
            Assert.AreEqual(TaskStatus.Failure, new FindTask { What = Perches(), StoreIn = PERCH }.Tick(far.Context, DT),
                "The perch is 20 m away: not one it runs past");

            GraphRig near = NewRig(out _, out _, new Vector3(3f, 0f, 0f));
            Target(near, new Vector3(-30f, 0f, 0f));
            Assert.AreEqual(TaskStatus.Failure, new FindTask { What = Perches(), StoreIn = PERCH }.Tick(near.Context, DT),
                "Its top is 30 m from the target: out of the bow's reach");
        }

        [Test]
        public void Archer_RunsPastAPerch_ClimbsAndShootsFromIt_TheTargetWalksOff_ComesDown()
        {
            GraphRig rig = NewRig(out FakeClimber climber, out Perch perch, new Vector3(4f, 0f, 0f));
            rig.ThinkInterval = 0f;
            Transform target = Target(rig, new Vector3(12f, 0f, 0f));
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            rig.GiveWeapon(Bow());
            Build(rig);

            Assert.IsTrue(rig.StepUntil(() => rig.Context.Occupied == perch.transform, 10f, STEP),
                "It climbs up and occupies the perch");
            Assert.IsTrue(rig.StepUntil(() => hits.Hits >= 2, 8f, STEP), "…and shoots from up there");
            Assert.IsTrue(perch.IsOn(rig.Actuator.Position), "Still up there between the shots");

            target.position = new Vector3(-20f, 0f, 0f);                // walks off: out of the bow's reach
            Assert.IsTrue(rig.StepUntil(() => !perch.IsOn(rig.Actuator.Position) && climber.ClimbDirection == 0,
                10f, STEP), "Comes down (the Climb decision)");
            Assert.AreEqual(2, climber.Climbs, "Up, and down again");
            Assert.IsNull(rig.Context.Occupied, "Off the perch: it occupies nothing");
        }

        // ---------- the rig ----------

        // An agent on flat ground with a perch (3 × 3 m top, 2.5 m high) at the origin, in its world's registry
        private GraphRig NewRig(out FakeClimber climber, out Perch perch, Vector3 at)
        {
            var actuator = new TestActuator { Position = at };
            var go = new GameObject(PERCH) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            perch = go.AddComponent<Perch>();
            perch.Height = 2.5f;
            perch.HalfSize = 1.5f;
            climber = new FakeClimber(actuator, perch);
            var rig = new GraphRig(trace: false, navigator: climber, actuator: actuator);
            rig.WakeUpAt(at);
            rig.UseObjects().Register(perch);
            return rig;
        }

        // A perch near by (one the shooter runs past) whose top is 3–14 m from the Brain's target
        private static WhatValue Perches() => new()
        {
            Component = typeof(Perch).FullName, Offer = Offer.Occupy, Radius = 6f, FromTarget = true, MinDistance = 3f,
            MaxDistance = 14f, Slack = 4f
        };

        private Transform Target(GraphRig rig, Vector3 at)
        {
            var go = new GameObject(TARGET) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            rig.Agent.Blackboard.Set(TARGET, go.transform);
            rig.Context.BrainTarget = go.transform;                     // the band's target, for the blocks ticked by hand
            return go.transform;
        }

        private static WeaponAsset Bow()
        {
            WeaponAsset bow = WeaponAsset.Create(new WeaponMove { Name = "Shot", Reach = 16f, WindUp = 0.3f, Recovery = 0.3f });
            bow.Shoots = true;
            bow.Shot.Pool = "";
            return bow;
        }

        // The archer: Search For a perch (1, on need: on nothing; Find → Climb To → Interact Occupy), Attack from it
        // (2, while it occupies one and the bow reaches), Climb (3: Climb To the target), Patrol (4)
        private static void Build(GraphRig rig)
        {
            var g = new BrainGraphBuilder();
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            g.Variable(PERCH, VariableType.Transform);
            AgentState search = g.Decision(
                new SearchForReasoner { Rest = 8f, Condition = new OccupiesSomething { Not = true } }, 1);
            g.Chain(search, new FindTask { What = Perches(), StoreIn = PERCH }, new ClimbToTask { Target = PERCH },
                new InteractTask { Object = PERCH, Interaction = Offer.Occupy });
            g.Chain(g.Decision(new ShootInReach { Condition = new OccupiesSomething() }, 2), new AttackTask());
            g.Chain(g.Decision(new ClimbReasoner(), 3), new ClimbToTask());
            g.Decision(new PatrolReasoner(), 4);
            rig.SetGraph(g.Build());
        }

        /// <summary>The Attack decision as the conditions lane makes it: the weapon in the hand reaches the Brain's target.</summary>
        private sealed class ShootInReach : ReasonerTask
        {
            public override bool Holds(in ReasonerQuery query, out string whyNot)
            {
                WeaponAsset weapon = query.Context?.Weapon;
                bool reaches = query.Facts.Sees && weapon != null && query.Facts.Distance <= weapon.ShortestReach;
                whyNot = reaches ? null : "out of the weapon's reach";
                return reaches;
            }
        }

        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                rig.Actuator.Apply(DT);
            }

            return answer;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
