using System.Collections.Generic;
using NUnit.Framework;
using AIKit.NavMeshNavigation;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A weapon that shoots is data like any other: the character carries the Attack node's shot out (AttackRunner) —
    /// the arrow leaves at the end of the draw (the move's wind-up) through the body's launcher, flies to where the
    /// target was and hits what it passes near; it asks the target for a place to shoot from in its own pool (§18.2
    /// p. 1) and gives it back after the shot, so the archers take turns. The same graph strikes with a blade and
    /// shoots with a bow: the slot is the graph's, the numbers and the delivery are the weapon's.
    /// </summary>
    public sealed class WeaponShotTests
    {
        private const string VICTIM = "Victim";
        private const string RANGED = "Ranged";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ArrowFlight arrow in Arrows())
                Object.DestroyImmediate(arrow.gameObject);
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
            GraphRig.DestroyAll();
        }

        private static WeaponMove Shot() =>
            new() { Name = "Shot", Reach = 14f, Damage = 2f, WindUp = 0.8f, Recovery = 0.5f, NeedsSight = true };

        private static WeaponAsset Bow(string pool = "")
        {
            var bow = WeaponAsset.Create(Shot());
            bow.Shoots = true;
            bow.Shot.Pool = pool;
            return bow;
        }

        private static ArrowFlight[] Arrows() =>
            Object.FindObjectsByType<ArrowFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // The Strike as the one block of a decision's chain. The graph is started in its Brain at once, so the Brain
        // takes the decision on the first step and the Strike runs from that step on — the timings below count from it
        private static void Start(GraphRig rig, AttackTask attack)
        {
            rig.SetGraph(GraphRig.OneState(attack));
            rig.Instance.Start(rig.Context);
        }

        [Test]
        public void Bow_TheArrowLeavesAtTheEndOfTheDraw_NotBefore()
        {
            WeaponMove shot = Shot();
            var rig = new GraphRig();
            rig.GiveWeapon(Bow());
            rig.Context.Launcher = ArrowLauncher.Shared;
            Victim(rig, new Vector3(0f, 0f, 8f));
            Start(rig, new AttackTask { Target = VICTIM, Move = shot.Name });

            rig.StepSeconds(shot.WindUp - 0.05f);
            Assert.That(Arrows(), Is.Empty, "the bow is still being drawn");

            rig.StepSeconds(0.1f);
            Assert.That(Arrows().Length, Is.EqualTo(1), "the arrow leaves when the bow is drawn");
        }

        [Test]
        public void Bow_TheArrowFliesToTheTarget_ItIsTheArrowThatHits()
        {
            WeaponMove shot = Shot();
            var rig = new GraphRig();
            rig.GiveWeapon(Bow());
            rig.Context.Launcher = ArrowLauncher.Shared;
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, 8f));
            Start(rig, new AttackTask { Target = VICTIM, Move = shot.Name });
            rig.StepSeconds(shot.WindUp + 0.05f);
            Assert.That(victim.Hits, Is.Zero, "the arrow has only just left");

            ArrowFlight arrow = Arrows()[0];
            Fly(arrow);

            Assert.That(arrow.Hit, Is.True);
            Assert.That(victim.Hits, Is.EqualTo(1));
            Assert.That(victim.LastDamage, Is.EqualTo(shot.Damage));
        }

        [Test]
        public void Bow_TheTargetStepsAsideAfterTheShot_TheArrowMisses()
        {
            WeaponMove shot = Shot();
            var rig = new GraphRig();
            rig.GiveWeapon(Bow());
            rig.Context.Launcher = ArrowLauncher.Shared;
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, 8f));
            Start(rig, new AttackTask { Target = VICTIM, Move = shot.Name });
            rig.StepSeconds(shot.WindUp + 0.05f);

            victim.transform.position = new Vector3(3f, 0f, 8f);         // a step aside while the arrow flies
            ArrowFlight arrow = Arrows()[0];
            Fly(arrow);

            Assert.That(arrow.Ended, Is.True, "out of its range");
            Assert.That(victim.Hits, Is.Zero);
        }

        [Test]
        public void Arrow_AWallOnTheWay_EndsThere()
        {
            var go = new GameObject("Arrow") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            Transform target = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave }.transform;
            _created.Add(target.gameObject);
            target.position = new Vector3(0f, 0f, 8f);
            HitProbe victim = target.gameObject.AddComponent<HitProbe>();
            go.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, 0f), Quaternion.identity);
            var arrow = go.AddComponent<ArrowFlight>();
            arrow.Launch(null, target, 1f, 20f, 20f, 0.7f, 1.2f, new Wall(4f));

            Fly(arrow);

            Assert.That(arrow.Ended, Is.True);
            Assert.That(victim.Hits, Is.Zero, "the wall stopped it");
            Assert.That(go.transform.position.z, Is.LessThanOrEqualTo(4f));
        }

        [Test]
        public void Bow_NoClearLine_DoesNotShoot()
        {
            var rig = new GraphRig();
            rig.GiveWeapon(Bow());
            rig.Context.Launcher = ArrowLauncher.Shared;
            rig.Context.Sight = new Wall(4f);
            Victim(rig, new Vector3(0f, 0f, 8f));
            var attack = new AttackTask { Target = VICTIM, Move = "Shot" };
            rig.SetGraph(GraphRig.OneState(attack));

            rig.StepSeconds(2f);

            Assert.That(Arrows(), Is.Empty);
            Assert.That(attack.StatusOf(rig.Context), Is.EqualTo(TaskStatus.Failure), "Out Of Reach: not from here");
        }

        [Test]
        public void Bow_OnePlaceToShootFrom_TwoArchersTakeTurns_NeverBothDrawAtOnce()
        {
            WeaponMove shot = Shot();
            var world = new AIWorld();
            Transform target = Target(new Vector3(0f, 0f, 8f), places: 1);
            GraphRig first = Archer(world, new Vector3(-1f, 0f, 0f), target);
            GraphRig second = Archer(world, new Vector3(1f, 0f, 0f), target);
            Engagement engagement = null;

            int most = 0;
            var shots = new HashSet<GraphRig>();
            float seconds = (shot.WindUp + shot.Recovery) * 3f;
            for (float time = 0f; time < seconds; time += DT)
            {
                world.BeginStep(DT);
                first.Step(DT);
                second.Step(DT);
                engagement ??= world.Roster.EngagementOf(target);
                if (engagement == null)
                    continue;

                int holding = (engagement.Tokens.Holds(first.Agent, RANGED) ? 1 : 0) +
                              (engagement.Tokens.Holds(second.Agent, RANGED) ? 1 : 0);
                most = Mathf.Max(most, holding);
                if (engagement.Tokens.Holds(first.Agent, RANGED))
                    shots.Add(first);
                if (engagement.Tokens.Holds(second.Agent, RANGED))
                    shots.Add(second);
            }

            Assert.That(most, Is.EqualTo(1), "one place: one archer draws at a time");
            Assert.That(shots.Count, Is.EqualTo(2), "the place goes back after a shot: the other one shoots too");
            Assert.That(Arrows().Length, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Bow_ItsPlaceIsNotAPlaceToFightFrom()
        {
            WeaponMove shot = Shot();
            var world = new AIWorld();
            Transform target = Target(new Vector3(0f, 0f, 8f), places: 1);
            GraphRig archer = Archer(world, Vector3.zero, target);
            Engagement engagement = null;
            for (float time = 0f; time < shot.WindUp * 0.5f; time += DT)
            {
                world.BeginStep(DT);
                archer.Step(DT);
                engagement ??= world.Roster.EngagementOf(target);
            }

            Assert.That(engagement, Is.Not.Null);
            Assert.That(engagement.Tokens.Holds(archer.Agent, RANGED), Is.True, "set-up: it draws");
            engagement.Tokens.NearestHolds(AttackSlots.POOL, AttackSlots.SWAP_MARGIN);   // the swords' places exist
            Assert.That(engagement.Tokens.HoldsPlace(archer.Agent), Is.False,
                "it still waits for a place for its sword (Circle holds)");
        }

        [Test]
        public void Shot_ABodyWithNoLauncher_TheShotLandsAtTheEndOfTheDraw_NoArrow()
        {
            WeaponMove shot = Shot();
            var rig = new GraphRig();
            rig.GiveWeapon(Bow());
            rig.Context.Launcher = null;
            HitProbe victim = Victim(rig, new Vector3(0f, 0f, 8f));
            Start(rig, new AttackTask { Target = VICTIM, Move = shot.Name });

            rig.StepSeconds(shot.WindUp - 0.05f);
            Assert.That(victim.Hits, Is.Zero);

            rig.StepSeconds(0.1f);
            Assert.That(victim.Hits, Is.EqualTo(1));
            Assert.That(Arrows(), Is.Empty);
        }

        [Test]
        public void Attack_OneGraph_ABladeStrikes_ABowShoots_TheSlotIsTheGraphs()
        {
            const string SLOT = "Attack 1";
            var blade = WeaponAsset.Create(new WeaponMove { Name = SLOT, Reach = 2f, WindUp = 0.3f, Recovery = 0.3f });
            WeaponAsset bow = Bow();
            bow.Find("Shot").Name = SLOT;

            foreach ((WeaponAsset weapon, Vector3 at, bool arrow) in new[]
                     {
                         (blade, new Vector3(0f, 0f, 1.5f), false),
                         (bow, new Vector3(0f, 0f, 8f), true)
                     })
            {
                var rig = new GraphRig();
                rig.GiveWeapon(weapon);
                rig.Context.Launcher = ArrowLauncher.Shared;
                HitProbe victim = Victim(rig, at);
                Start(rig, new AttackTask { Target = VICTIM, Move = SLOT });

                rig.StepSeconds(weapon.Find(SLOT).WindUp + 0.05f);
                foreach (ArrowFlight flying in Arrows())
                    Fly(flying);

                Assert.That(victim.Hits, Is.EqualTo(1), weapon == bow ? "the bow's arrow hit" : "the blade hit");
                Assert.That(Arrows().Length > 0, Is.EqualTo(arrow), "only the weapon that shoots lets an arrow go");
                foreach (ArrowFlight flying in Arrows())
                    Object.DestroyImmediate(flying.gameObject);
                GraphRig.DestroyAll();
            }
        }

        private GraphRig Archer(IAIWorld world, Vector3 at, Transform target)
        {
            var rig = new GraphRig(trace: false, world: world) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            rig.GiveWeapon(Bow(RANGED));
            rig.Context.Launcher = ArrowLauncher.Shared;
            rig.Agent.Blackboard.Set(VICTIM, target);
            rig.SetGraph(GraphRig.OneState(new AttackTask { Target = VICTIM, Move = "Shot" }));
            return rig;
        }

        private Transform Target(Vector3 at, int places)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            go.AddComponent<AttackSlotCapacity>().SetPool(RANGED, places);
            go.AddComponent<HitProbe>();
            return go.transform;
        }

        // The arrow flies its whole way, step by step
        private static void Fly(ArrowFlight arrow)
        {
            for (int i = 0; i < 500 && !arrow.Ended; i++)
                arrow.Fly(DT);
        }

        private static HitProbe Victim(GraphRig rig, Vector3 at)
        {
            Transform victim = rig.CreateTargetAt(at);
            rig.Context.Blackboard.Set(VICTIM, victim);
            return victim.gameObject.AddComponent<HitProbe>();
        }

        /// <summary>A wall across the way at this z: a line that crosses it is blocked.</summary>
        private sealed class Wall : ISightProbe
        {
            private readonly float _z;

            public Wall(float z) => _z = z;

            public bool HasLineOfSight(Vector3 from, Vector3 to) =>
                !(Mathf.Min(from.z, to.z) <= _z && Mathf.Max(from.z, to.z) >= _z);
        }
    }
}
