using AIKit;
using NUnit.Framework;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>
    /// The whole elite on a synthetic level (<see cref="AITestRig"/>): its graph, its body on the grid, the level's step.
    /// </summary>
    public sealed class EliteAgentTests
    {
        private const float DT = 1f / 30f;

        private AITestRig _rig;

        [SetUp]
        public void SetUp() => _rig = new AITestRig();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        /// <summary>Steps until a leap has started and ended (at most <paramref name="seconds"/>); where it landed.</summary>
        private Vector3 LandingAfterLeap(AITestRig.Elite elite, float seconds)
        {
            EliteMotor motor = elite.Behaviour.Body.Motor;
            bool leapt = false;
            for (float t = 0f; t < seconds; t += DT)
            {
                _rig.Step(DT);
                leapt |= motor.IsLeaping;
                if (leapt && !motor.IsLeaping)
                    return elite.Transform.position;
            }

            Assert.Fail(leapt ? "the leap did not end" : "it did not leap");
            return default;
        }

        [Test]
        public void NoTank_ItWandersNearItsSpawn()
        {
            AITestRig.Elite elite = _rig.AddElite(new Vector3(0f, 0f, 0f), Vector3.forward);

            float farthest = 0f;
            bool moved = false;
            for (float t = 0f; t < 8f; t += DT)
            {
                _rig.Step(DT);
                float d = new Vector2(elite.Transform.position.x, elite.Transform.position.z).magnitude;
                farthest = Mathf.Max(farthest, d);
                moved |= d > 0.5f;
            }

            Assert.IsTrue(moved, "it walks about");
            Assert.That(farthest, Is.LessThanOrEqualTo(EliteBrain.WANDER_RADIUS + 1f), "and stays near its spawn");
            Assert.That(elite.Character.Graph.Decision.DisplayName, Is.EqualTo("Wander"));
        }

        [Test]
        public void SeesTheTank_GoesRoundAWall_AndBitesIt()
        {
            _rig.AddTank(new Vector3(10f, 0f, 0f));
            _rig.Wall(new Vector2(0f, -6f), new Vector2(1f, 6f));      // a house between them
            AITestRig.Elite elite = _rig.AddElite(new Vector3(-10f, 0f, 0f), Vector3.right);

            float maxZ = 0f;
            for (float t = 0f; t < 15f && elite.Fired == 0; t += DT)
            {
                _rig.Step(DT);
                maxZ = Mathf.Max(maxZ, Mathf.Abs(elite.Transform.position.z));
                Assert.That(elite.Transform.position.x > 0f && elite.Transform.position.x < 1f &&
                            Mathf.Abs(elite.Transform.position.z) < 6f, Is.False, "never inside the wall");
            }

            Assert.That(elite.Fired, Is.GreaterThan(0), "it bit the tank");
            Assert.That(maxZ, Is.GreaterThan(6f), "round the end of the wall");
            Assert.That(_rig.Tank.DistanceToHull(elite.Transform.position), Is.LessThanOrEqualTo(RegaxBiteRunner.REACH));
            Assert.That(_rig.Tank.DistanceToHull(elite.Transform.position), Is.GreaterThanOrEqualTo(EliteMotor.BODY_CLEARANCE - 0.05f),
                "against the hull, not in it");
        }

        [Test]
        public void ABlastNearby_StunThenLeapAway()
        {
            AITestRig.Elite elite = _rig.AddElite(new Vector3(0f, 0f, 0f), Vector3.forward);
            _rig.Run(0.3f);
            Vector3 start = elite.Transform.position;
            Vector3 blast = start + new Vector3(1.5f, 0f, 0.5f);

            ExplosionEvents.Raise(blast, 3f);
            _rig.Run(0.3f);
            Assert.That(Vector3.Distance(elite.Transform.position, start), Is.LessThan(0.2f), "stunned first");

            Vector3 end = LandingAfterLeap(elite, 1.5f);
            Vector3 away = start - blast;
            away.y = 0f;
            Vector3 moved = end - start;
            moved.y = 0f;
            Assert.That(moved.magnitude, Is.InRange(2.8f, 5.5f), "leapt 3-5 m");
            Assert.That(Vector3.Angle(moved, away), Is.LessThan(35f), "away from the blast");
            Assert.That(end.y, Is.EqualTo(0f).Within(0.05f), "landed on the ground");
        }

        [Test]
        public void ABlastFarOff_Ignored()
        {
            AITestRig.Elite elite = _rig.AddElite(new Vector3(0f, 0f, 0f), Vector3.forward);
            _rig.Run(0.3f);
            int blasts = elite.Character.Blackboard.Get<int>(EliteBrain.BLAST_COUNT);

            ExplosionEvents.Raise(new Vector3(8f, 0f, 0f), 3f);    // 8 m off a 3 m blast: beyond 1.5 radii

            Assert.That(elite.Character.Blackboard.Get<int>(EliteBrain.BLAST_COUNT), Is.EqualTo(blasts));
        }

        [Test]
        public void ABlastBesideTheTank_ItLeapsClearOfTheHull_AndComesBack()
        {
            _rig.AddTank(new Vector3(5f, 0f, 0f));
            AITestRig.Elite elite = _rig.AddElite(new Vector3(-6f, 0f, 0f), Vector3.right);
            for (float t = 0f; t < 10f && elite.Fired == 0; t += DT)
                _rig.Step(DT);
            Assert.That(elite.Fired, Is.GreaterThan(0));

            Vector3 at = elite.Transform.position;
            ExplosionEvents.Raise(at + new Vector3(-1f, 0f, 0f), 4f);     // behind it: away is into the tank
            Vector3 landed = LandingAfterLeap(elite, 1.5f);

            Assert.That(Vector3.Distance(landed, at), Is.GreaterThan(2.5f), "it leapt");
            Assert.That(_rig.Tank.DistanceToHull(landed), Is.GreaterThanOrEqualTo(EliteMotor.BODY_CLEARANCE - 0.05f),
                "clear of the hull");

            int bites = elite.Fired;
            _rig.Run(6f);
            Assert.That(elite.Fired, Is.GreaterThan(bites), "back to the fight");
        }

        [Test]
        public void AHitWithoutABlast_Flinches_AndKnowsTheTank()
        {
            _rig.AddTank(new Vector3(15f, 0f, 15f));
            AITestRig.Elite elite = _rig.AddElite(new Vector3(-15f, 0f, -15f), Vector3.back);   // facing away
            _rig.Run(0.2f);

            var shot = new Atomic.Entities.Entity("Shell");
            shot.AddDamage(new Atomic.Elements.ReactiveVariable<int>(20));
            elite.Entity.GetDamageTakenEvent().Invoke(new TakeDamageArgs(shot));
            bool flinched = false;
            for (float t = 0f; t < 0.6f; t += DT)
            {
                _rig.Step(DT);
                flinched |= elite.Character.Graph.Decision != null && elite.Character.Graph.Decision.DisplayName == "Got Hit";
            }

            Assert.IsTrue(flinched);
            Assert.That(elite.Character.Context.Hits.Count, Is.EqualTo(1));
            Assert.That(elite.Character.Context.Perception.Level, Is.EqualTo(AlertLevel.Aware), "the hit made the tank known");
        }

        [Test]
        public void Step_AllocatesNothing_AfterWarmUp()
        {
            _rig.AddTank(new Vector3(12f, 0f, 3f));
            _rig.Wall(new Vector2(0f, -6f), new Vector2(1f, 6f));
            _rig.AddElite(new Vector3(-12f, 0f, 0f), Vector3.right);
            _rig.AddElite(new Vector3(-12f, 0f, 4f), Vector3.right);
            _rig.Run(3f);

            Assert.That(() =>
            {
                for (int i = 0; i < 30; i++)
                    _rig.Step(DT);
            }, NoAllocation.Constraint);
        }
    }
}
