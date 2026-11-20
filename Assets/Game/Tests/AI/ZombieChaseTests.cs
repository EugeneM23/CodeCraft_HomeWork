using NUnit.Framework;
using UnityEngine;

namespace Game.AIKit.Adapters.Tests
{
    /// <summary>
    /// The skinned zombie's brain (<see cref="ZombieChaseBehaviour"/>) on a synthetic level (<see cref="AITestRig"/>):
    /// it goes for the tank round walls, takes a place round the hull, bites, and the zombies keep apart.
    /// </summary>
    public sealed class ZombieChaseTests
    {
        private const float DT = 1f / 30f;

        private AITestRig _rig;

        [SetUp]
        public void SetUp() => _rig = new AITestRig();

        [TearDown]
        public void TearDown() => _rig.Dispose();

        [Test]
        public void NoTank_ItStands()
        {
            AITestRig.Zombie zombie = _rig.AddZombie(new Vector3(0f, 0f, 0f));
            _rig.Run(2f, DT);
            Assert.That(Flat(zombie.Transform.position).magnitude, Is.LessThan(0.05f));
        }

        [Test]
        public void GoesRoundAWall_ToTheTank_AndBitesIt()
        {
            _rig.AddTank(new Vector3(10f, 0f, 0f));
            _rig.Wall(new Vector2(0f, -6f), new Vector2(1f, 6f));      // a house between them
            AITestRig.Zombie zombie = _rig.AddZombie(new Vector3(-10f, 0f, 0f));

            float maxZ = 0f;
            for (float t = 0f; t < 15f && zombie.Fired == 0; t += DT)
            {
                _rig.Step(DT);
                Vector3 p = zombie.Transform.position;
                maxZ = Mathf.Max(maxZ, Mathf.Abs(p.z));
                Assert.That(p.x > 0f && p.x < 1f && Mathf.Abs(p.z) < 6f, Is.False, "never inside the wall");
            }

            Assert.That(zombie.Fired, Is.GreaterThan(0), "it bit the tank");
            Assert.That(maxZ, Is.GreaterThan(6f), "round the end of the wall");
            float gap = _rig.Tank.DistanceToHull(zombie.Transform.position);
            Assert.That(gap, Is.LessThanOrEqualTo(RegaxBiteRunner.REACH), "within the bite's reach");
            Assert.That(gap, Is.GreaterThanOrEqualTo(EliteMotor.BODY_CLEARANCE - 0.05f), "against the hull, not in it");
        }

        [Test]
        public void ACrowd_SpreadsRoundTheHull_OnePerPlace_AndNeverOverlaps()
        {
            _rig.AddTank(new Vector3(0f, 0f, 0f));
            const int count = 12;
            for (int i = 0; i < count; i++)
                _rig.AddZombie(new Vector3(-12f + (i % 4) * 0.4f, 0f, -1f + (i / 4) * 0.4f));   // a tight bunch on one side

            float closest = float.MaxValue;
            for (float t = 0f; t < 12f; t += DT)
            {
                _rig.Step(DT);
                if (t > 2f)  // the bunch they start in has come apart
                    closest = Mathf.Min(closest, ClosestPair());
            }

            Assert.That(closest, Is.GreaterThan(0.95f), "no two zombies walk through each other (their capsules are 1 m wide)");
            var used = new bool[ZombieRing.Count];
            var sides = new bool[4];                           // right, left, front, back of the 3.5 × 6.5 m hull
            int biting = 0;
            foreach (AITestRig.Zombie zombie in _rig.Zombies)
            {
                int slot = zombie.Behaviour.RingSlot;
                Assert.That(slot, Is.GreaterThanOrEqualTo(0), "each has a place round the hull");
                Assert.That(used[slot], Is.False, $"place {slot} is one zombie's");
                used[slot] = true;
                Vector3 at = Flat(zombie.Transform.position);
                bool across = Mathf.Abs(at.x) - 1.75f > Mathf.Abs(at.z) - 3.25f;
                sides[across ? (at.x > 0f ? 0 : 1) : (at.z > 0f ? 2 : 3)] = true;
                if (zombie.Fired > 0)
                    biting++;
                Assert.That(_rig.Tank.DistanceToHull(zombie.Transform.position), Is.LessThanOrEqualTo(RegaxBiteRunner.REACH),
                    "at the hull");
            }

            Assert.That(biting, Is.EqualTo(count), "every one bites");
            int taken = (sides[0] ? 1 : 0) + (sides[1] ? 1 : 0) + (sides[2] ? 1 : 0) + (sides[3] ? 1 : 0);
            Assert.That(taken, Is.GreaterThanOrEqualTo(3), "spread round the hull, not heaped on the side they came from");
        }

        [Test]
        public void ADeadZombie_GivesUpItsPlace_AndStops()
        {
            _rig.AddTank(new Vector3(0f, 0f, 0f));
            AITestRig.Zombie zombie = _rig.AddZombie(new Vector3(-6f, 0f, 0f));
            _rig.Run(4f, DT);
            Assert.That(zombie.Behaviour.RingSlot, Is.GreaterThanOrEqualTo(0));

            zombie.Health.Reduce(1000);
            _rig.Run(0.2f, DT);
            Assert.That(zombie.Behaviour.RingSlot, Is.EqualTo(-1));
            Assert.That(zombie.Behaviour.CanHoldPlace, Is.False);
            Vector3 at = zombie.Transform.position;
            _rig.Run(1f, DT);
            Assert.That(Vector3.Distance(at, zombie.Transform.position), Is.LessThan(0.01f), "the dead do not walk");
        }

        private float ClosestPair()
        {
            float closest = float.MaxValue;
            for (int i = 0; i < _rig.Zombies.Count; i++)
            for (int j = i + 1; j < _rig.Zombies.Count; j++)
                closest = Mathf.Min(closest,
                    Flat(_rig.Zombies[i].Transform.position - _rig.Zombies[j].Transform.position).magnitude);
            return closest;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
