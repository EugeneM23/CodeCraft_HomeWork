using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The distances of step 90 (spec 10.4; handbook §18.3: the numbers of the distances are the role's — here, the
    /// weapon's): 0 in a distance means "from the weapon in the hand" (its shortest move), each node its share of it —
    /// Chase / Flank stop at 0.8, Flank's way round and the radius of Square Off / Circle Target are 1.1, Kite keeps 0.35
    /// to 0.65; with no weapon the node's old default; and the node says so.
    /// </summary>
    public sealed class WeaponDistanceTests
    {
        private const string TARGET = "Target";
        private const float REACH = 3.5f;
        private readonly List<Object> _created = new();
        private GraphRig _rig;

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _rig.GiveWeapon(WeaponAsset.Create(
                new WeaponMove { Name = "Heavy", Reach = 5f },
                new WeaponMove { Name = "Normal", Reach = REACH }));
        }

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
        public void Reach_IsTheWeaponsShortestMove_NoWeapon_TheFallback()
        {
            Assert.AreEqual(REACH, WeaponDistances.ReachOf(_rig.Context), "Near enough for every move of it to land");
            Assert.AreEqual(2f, WeaponDistances.Resolve(2f, _rig.Context, 0.8f, 1.5f), "A number of its own is as it is");
            Assert.AreEqual(REACH * 0.8f, WeaponDistances.Resolve(0f, _rig.Context, 0.8f, 1.5f), 1e-5f);

            _rig.GiveWeapon(null);
            Assert.AreEqual(1.5f, WeaponDistances.Resolve(0f, _rig.Context, 0.8f, 1.5f), "No weapon: the old default");
        }

        [Test]
        public void ChaseAndFlank_StopZero_StopAtFourFifthsOfTheReach_FlankBypassZero_GoesRoundAtElevenTenths()
        {
            var chase = new ChaseTask { StopDistance = 0f };
            Assert.AreEqual(REACH * WeaponDistances.STOP, chase.StopFor(_rig.Context), 1e-5f);
            StringAssert.Contains("0.8 × my weapon's reach (no weapon: 1.5 m)", chase.Requirement);

            var walk = CloseIn(new ChaseTask { Target = TARGET, StopDistance = 0f, FinishWhenCaught = true });
            Assert.AreEqual(REACH * WeaponDistances.STOP, walk, 0.3f, "It stands well inside its reach");

            var flank = new FlankTask { BypassRadius = 0f };
            StringAssert.Contains("goes round at 1.1 × my weapon's reach (no weapon: 3 m)", flank.Requirement);
            Assert.AreEqual(1.5f, new ChaseTask().StopFor(_rig.Context), "A number of its own: as it is");
        }

        [Test]
        public void SquareOffAndCircleTarget_RadiusZero_JustOutsideTheReach_AndTheNodesSaySo()
        {
            StringAssert.Contains("1.1 × my weapon's reach (no weapon: 4 m)", new SquareOffTask { Radius = 0f }.Requirement);
            StringAssert.Contains("1.1 × my weapon's reach (no weapon: 4 m)", new CircleTargetTask { Radius = 0f }.Requirement);
            Assert.IsFalse(new SquareOffTask { Radius = 4f }.Requirement.Contains("reach"), "Its own number: nothing to say");

            float kept = Circle(new CircleTargetTask { Target = TARGET, Radius = 0f, WaitForSlot = false, Duration = 100f,
                Sway = 0f, SpeedScale = 1f });
            Assert.AreEqual(REACH * WeaponDistances.AROUND, kept, 0.4f, "It circles just outside its reach");
        }

        [Test]
        public void Kite_BandZero_FromTheShotsReach_AndTheNodeSaysSo()
        {
            var kite = new KiteTask { MinDistance = 0f, MaxDistance = 0f };
            StringAssert.Contains("backs away within 0.35 × my weapon's reach", kite.Requirement);
            StringAssert.Contains("comes closer beyond 0.65 × my weapon's reach", kite.Requirement);

            Transform target = Target(new Vector3(1f, 0f, 0f));
            _rig.Actuator.Position = Vector3.zero;
            var backing = new KiteTask { Target = TARGET, MinDistance = 0f, MaxDistance = 0f, NeedsSight = false, WatchTheWayBack = false };
            for (int i = 0; i < 100; i++)
            {
                if (backing.Tick(_rig.Context, 0.05f) != TaskStatus.Running)
                    break;
                _rig.Actuator.Apply(0.05f);
            }

            float distance = Vector3.Distance(_rig.Actuator.Position, target.position);
            Assert.GreaterOrEqual(distance, REACH * WeaponDistances.KITE_NEAR - 0.3f,
                "Too near for the band of its weapon: it backed away");
        }

        [Test]
        public void Attack_CloserZero_TheDecisionSaysWithinMyWeaponsReach()
        {
            var attack = new FightReasoner { Closer = 0f };
            Assert.AreEqual(REACH, attack.CloserFor(_rig.Context), 1e-5f);
            StringAssert.Contains("taken within my weapon's reach (no weapon: 1.9 m)", attack.Requirement);
        }

        // ---------- helpers ----------

        private Transform Target(Vector3 at)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            _rig.Agent.Blackboard.Set(TARGET, go.transform);
            return go.transform;
        }

        // Where a chase that ends when caught stands from the target
        private float CloseIn(ChaseTask chase)
        {
            Transform target = Target(new Vector3(10f, 0f, 0f));
            _rig.Actuator.Position = Vector3.zero;
            for (int i = 0; i < 400; i++)
            {
                if (chase.Tick(_rig.Context, 0.02f) != TaskStatus.Running)
                    break;
                _rig.Actuator.Apply(0.02f);
            }

            return Vector3.Distance(_rig.Actuator.Position, target.position);
        }

        // How far from the target a circling body keeps, on average over its last second
        private float Circle(CircleTargetTask circle)
        {
            Transform target = Target(Vector3.zero);
            _rig.Actuator.Position = new Vector3(2f, 0f, 0f);
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < 400; i++)
            {
                circle.Tick(_rig.Context, 0.02f);
                _rig.Actuator.Apply(0.02f);
                if (i >= 350)
                {
                    sum += Vector3.Distance(_rig.Actuator.Position, target.position);
                    count++;
                }
            }

            return sum / count;
        }
    }
}
