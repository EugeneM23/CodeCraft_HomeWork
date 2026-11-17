using System.Collections.Generic;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The lob of the one launcher (step 90 — a thrown rock, a grenade): it comes down where the target stood when it
    /// was let go, hits it there within the shot's hit radius and misses one that stepped aside, tells its landing once
    /// with the point, and a model taken from the hand leaves the hand and is what flies. A lob over low cover is not
    /// held back by the straight line a shot would take.
    /// </summary>
    public sealed class ArcFlightTests
    {
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (ArcFlight flight in Object.FindObjectsByType<ArcFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(flight.gameObject);
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = at;
            _created.Add(go);
            return go.transform;
        }

        private static WeaponShot Lob() => new() { Arc = 0.15f, Speed = 14f, HitRadius = 1f, Height = 1.4f, Pool = "" };

        private static ArcFlight Flight()
        {
            ArcFlight[] flights = Object.FindObjectsByType<ArcFlight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, flights.Length, "One lob in the air");
            return flights[0];
        }

        private static void Land(ArcFlight flight)
        {
            for (int i = 0; i < 1000 && !flight.Ended; i++)
                flight.Fly(DT);
        }

        [Test]
        public void Lob_ComesDownWhereTheTargetStood_HitsItThere()
        {
            Transform shooter = Thing("Thrower", Vector3.zero);
            Transform target = Thing("Target", new Vector3(0f, 0f, 8f));
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            WeaponShot shot = Lob();

            ArrowLauncher.Shared.Launch(new ProjectileLaunch(shooter, target, 2f, shot, null));
            ArcFlight flight = Flight();
            Land(flight);

            Assert.IsTrue(flight.Ended);
            Assert.IsTrue(flight.Hit);
            Assert.AreEqual(1, hits.Hits);
            Assert.AreEqual(2f, hits.LastDamage);
            Assert.AreEqual(new Vector3(0f, 0f, 8f), flight.LandsAt, "Where the target stood when it was let go");
        }

        [Test]
        public void Lob_TheTargetSteppedAsideBeyondTheHitRadius_Misses()
        {
            Transform shooter = Thing("Thrower", Vector3.zero);
            Transform target = Thing("Target", new Vector3(0f, 0f, 8f));
            HitProbe hits = target.gameObject.AddComponent<HitProbe>();
            WeaponShot shot = Lob();

            ArrowLauncher.Shared.Launch(new ProjectileLaunch(shooter, target, 2f, shot, null));
            target.position = new Vector3(shot.HitRadius + 0.5f, 0f, 8f);       // a step aside while it flies
            ArcFlight flight = Flight();
            Land(flight);

            Assert.IsFalse(flight.Hit);
            Assert.AreEqual(0, hits.Hits);
            Assert.AreEqual(new Vector3(0f, 0f, 8f), flight.LandsAt, "It still comes down where the target stood");
        }

        [Test]
        public void Lob_TellsItsLandingOnce_WithThePoint_AndWhomItStruck()
        {
            Transform shooter = Thing("Thrower", Vector3.zero);
            Transform target = Thing("Target", new Vector3(3f, 0f, 6f));
            var landing = new Landing();

            ArrowLauncher.Shared.Launch(new ProjectileLaunch(shooter, target, target.position, 0f, Lob(), null, 0.15f, 0f,
                null, null, landing, 7));
            ArcFlight flight = Flight();
            Land(flight);
            flight.Fly(DT);

            Assert.AreEqual(1, landing.Points.Count, "Told once");
            Assert.AreEqual(new Vector3(3f, 0f, 6f), landing.Points[0]);
            Assert.AreSame(target, landing.Struck[0]);
            Assert.AreEqual(7, landing.Tokens[0], "The token of the launch comes back with it");
        }

        [Test]
        public void Lob_AtAPoint_ComesDownThere_InItsOwnTime()
        {
            Transform shooter = Thing("Thrower", Vector3.zero);
            var landing = new Landing();
            var point = new Vector3(-4f, 0f, 5f);
            const float FLIGHT = 1f;

            ArrowLauncher.Shared.Launch(new ProjectileLaunch(shooter, null, point, 0f, Lob(), null, 0.3f, FLIGHT, null, null,
                landing, 0));
            ArcFlight flight = Flight();
            int steps = 0;
            while (!flight.Ended && steps < 1000)
            {
                flight.Fly(DT);
                steps++;
            }

            Assert.AreEqual(FLIGHT, steps * DT, DT + 1e-4f, "In the air for the launch's own time");
            Assert.AreEqual(point, landing.Points[0]);
            Assert.IsNull(landing.Struck[0], "At a point: nobody struck by the flight itself");
        }

        [Test]
        public void Lob_AModelFromTheHand_LeavesTheHand_AndIsWhatFlies()
        {
            Transform shooter = Thing("Thrower", Vector3.zero);
            Transform hand = Thing("Hand", new Vector3(0.3f, 1.2f, 0.2f));
            hand.SetParent(shooter, true);
            Transform rock = Thing("Rock", Vector3.zero);
            rock.SetParent(hand, false);
            Transform target = Thing("Target", new Vector3(0f, 0f, 8f));

            ArrowLauncher.Shared.Launch(new ProjectileLaunch(shooter, target, target.position, 1f, Lob(), null, 0.15f, 0f,
                rock, null, null, 0));

            Assert.IsNull(rock.parent, "Out of the hand");
            Assert.IsNotNull(rock.GetComponent<ArcFlight>(), "The rock itself flies");
            Assert.AreEqual(new Vector3(0.3f, 1.2f, 0.2f), rock.position, "From where the hand held it");
        }

        [Test]
        public void Lob_OverLowCover_CanFly_WhereAStraightShotCannot()
        {
            Transform shooter = Thing("Thrower", Vector3.zero);
            Transform target = Thing("Target", new Vector3(0f, 0f, 8f));
            var crate = new LowWall(z: 4f, height: 2f);
            var straight = new WeaponShot { Arc = 0f, Height = 1.4f, AimHeight = 1.2f, Clearance = 0f, Pool = "" };
            var lobbed = new WeaponShot { Arc = 0.3f, Height = 1.4f, AimHeight = 1.2f, Pool = "" };

            Assert.IsFalse(ArrowLauncher.Shared.CanFly(new ProjectileLaunch(shooter, target, 1f, straight, crate)),
                "A straight line hits the crate");
            Assert.IsTrue(ArrowLauncher.Shared.CanFly(new ProjectileLaunch(shooter, target, 1f, lobbed, crate)),
                "The lob passes over it (its arc, not the straight line)");
            Assert.IsFalse(ArrowLauncher.Shared.CanFly(new ProjectileLaunch(shooter, target, 1f, lobbed, new LowWall(4f, 10f))),
                "…but not over a wall higher than its arc");
        }

        /// <summary>A wall across the way at z, this high: a line that crosses z below its top is blocked.</summary>
        private sealed class LowWall : ISightProbe
        {
            private readonly float _z;
            private readonly float _height;

            public LowWall(float z, float height)
            {
                _z = z;
                _height = height;
            }

            public bool HasLineOfSight(Vector3 from, Vector3 to)
            {
                if (Mathf.Min(from.z, to.z) > _z || Mathf.Max(from.z, to.z) < _z || Mathf.Approximately(from.z, to.z))
                    return true;

                float t = (_z - from.z) / (to.z - from.z);
                return Mathf.Lerp(from.y, to.y, t) > _height;
            }
        }

        /// <summary>Writes down where the launches came down.</summary>
        private sealed class Landing : IProjectileLanding
        {
            public readonly List<Vector3> Points = new();
            public readonly List<Transform> Struck = new();
            public readonly List<int> Tokens = new();

            public void Landed(in ProjectileLaunch launch, Vector3 point, Transform struck)
            {
                Points.Add(point);
                Struck.Add(struck);
                Tokens.Add(launch.Token);
            }
        }
    }
}
