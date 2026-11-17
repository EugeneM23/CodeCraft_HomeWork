using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// The sensors of the canvas: what each shape sees (Radius, Radius with a line of sight, Cone, Zone), how an object
    /// once seen is held and lost, how often the sensor looks, and Detection with several shapes wired in. The scene is
    /// stood in for: the objects of a kind come from a list, the walls from a probe the test sets.
    /// </summary>
    public sealed class SensorNodeTests
    {
        private const string SEEN = "Target";
        private const float DT = 0.05f;

        private GraphRig _rig;
        private readonly ListFinder _finder = new();
        private readonly Walls _walls = new();
        private readonly List<GameObject> _created = new();

        [SetUp]
        public void SetUp()
        {
            _rig = new GraphRig(trace: false);
            _rig.Context.Objects = _finder;
            _rig.Context.Sight = _walls;
            // A body in 3D looking where its transform does (+Z): the Cone opens where the body looks (step-90 recheck:
            // the body's facing, as the Senses — the test body is a side-view one looking along +X otherwise)
            _rig.Actuator.LooksIn3D = true;
            _rig.Actuator.Face(_rig.Context.Self.forward);
            _finder.Objects.Clear();
            _walls.Blocked.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject created in _created)
                Object.DestroyImmediate(created);
            _created.Clear();
            GraphRig.DestroyAll();
        }

        // ---------- Radius ----------

        [Test]
        public void Radius_NoticesWithinRadius_KeepsUpToLoseRadius_ThenLets_Go()
        {
            var sensor = new RadiusSensorTask { StoreIn = SEEN, Radius = 5f, LoseRadius = 7f, Interval = 0f };
            Transform player = Thing(new Vector3(6f, 0f, 0f));

            Look(sensor);
            Assert.IsNull(Seen(), "Farther than Radius: not noticed");

            player.position = new Vector3(4.9f, 0f, 0f);
            Look(sensor);
            Assert.AreSame(player, Seen());

            player.position = new Vector3(6.9f, 0f, 0f);
            Look(sensor);
            Assert.AreSame(player, Seen(), "Once seen, kept up to Lose Radius");

            player.position = new Vector3(7.1f, 0f, 0f);
            Look(sensor);
            Assert.IsNull(Seen(), "Beyond Lose Radius: let go");
        }

        [Test]
        public void Radius_HoldsWhatItSees_NotANearerOne_AndOfNewOnesTakesTheNearest()
        {
            var sensor = new RadiusSensorTask { StoreIn = SEEN, Radius = 10f, Interval = 0f };
            Transform far = Thing(new Vector3(8f, 0f, 0f));
            Transform near = Thing(new Vector3(3f, 0f, 0f));

            Look(sensor);
            Assert.AreSame(near, Seen(), "Of two new ones, the nearest");

            far.position = new Vector3(1f, 0f, 0f);
            Look(sensor);
            Assert.AreSame(near, Seen(), "It does not jump to a nearer one while it still sees its own");
        }

        [Test]
        public void Sensor_LooksEveryInterval_NotEveryStep()
        {
            const float INTERVAL = 0.5f;
            var sensor = new RadiusSensorTask { StoreIn = SEEN, Radius = 10f, Interval = INTERVAL };
            Look(sensor);                                   // the first look is at once
            Transform player = Thing(new Vector3(2f, 0f, 0f));

            float noticed = 0f;
            for (float t = DT; t <= 2f * INTERVAL && noticed == 0f; t += DT)
            {
                Look(sensor);
                if (Seen() != null)
                    noticed = t;
            }

            Assert.AreEqual(INTERVAL, noticed, DT + 1e-4f, "Seen on the next look, not on the next step");
            Assert.IsNotNull(player);
        }

        [Test]
        public void Sensor_NoVariableToWriteInto_WritesNothing()
        {
            var sensor = new RadiusSensorTask { StoreIn = "", Radius = 10f, Interval = 0f };
            Thing(new Vector3(1f, 0f, 0f));

            Assert.AreEqual(TaskStatus.Running, sensor.Tick(_rig.Context, DT), "A sensor runs beside everything, always");
            Assert.IsFalse(_rig.Context.Blackboard.Has(SEEN));
        }

        // ---------- Radius (Line Of Sight) ----------

        [Test]
        public void SightRadius_BehindAWall_NotNoticed_AHeldOneKeptForLoseSightAfter_ThenLost()
        {
            const float GRACE = 1f;
            var sensor = new SightRadiusSensorTask { StoreIn = SEEN, Radius = 10f, LoseSightAfter = GRACE, Interval = 0f };
            Transform player = Thing(new Vector3(3f, 0f, 0f));
            _walls.Blocked.Add(player);

            Look(sensor);
            Assert.IsNull(Seen(), "Behind a wall: not noticed");

            _walls.Blocked.Clear();
            Look(sensor);
            Assert.AreSame(player, Seen());

            _walls.Blocked.Add(player);
            Steps(sensor, GRACE - 0.2f);
            Assert.AreSame(player, Seen(), "Kept a while after the line got blocked: a chase is not dropped at a corner");
            Steps(sensor, 0.4f);
            Assert.IsNull(Seen(), "Then lost");
        }

        [Test]
        public void SightRadius_AnotherObjectInSight_DoesNotKeepAHeldOneBehindAWall()
        {
            const float GRACE = 1f;
            var sensor = new SightRadiusSensorTask { StoreIn = SEEN, Radius = 10f, LoseSightAfter = GRACE, Interval = 0f };
            Transform held = Thing(new Vector3(3f, 0f, 0f));
            Look(sensor);
            Assert.AreSame(held, Seen());

            Thing(new Vector3(-6f, 0f, 0f));                // in sight all the while, farther
            _walls.Blocked.Add(held);
            Steps(sensor, GRACE + 0.2f);

            Assert.AreNotSame(held, Seen(), "Its grace counts its own line only (handbook §9.4)");
        }

        // ---------- Cone ----------

        [Test]
        public void Cone_NoticesInFrontOnly_ButKeepsASeenOneBehind()
        {
            var sensor = new ConeSensorTask { StoreIn = SEEN, Range = 8f, HalfAngle = 45f, LoseRange = 10f, Interval = 0f };
            Vector3 forward = _rig.Context.Self.forward;
            Transform player = Thing(-forward * 3f);

            Look(sensor);
            Assert.IsNull(Seen(), "Behind the agent: not noticed");

            player.position = forward * 3f;
            Look(sensor);
            Assert.AreSame(player, Seen());

            player.position = -forward * 3f;
            Look(sensor);
            Assert.AreSame(player, Seen(), "Kept whichever way the agent faces: it does not lose what it chases by turning");
        }

        [Test]
        public void Cone_WithLineOfSight_ABlockedLookDoesNotSee()
        {
            var sensor = new ConeSensorTask { StoreIn = SEEN, HalfAngle = 180f, LineOfSight = true, Interval = 0f };
            Transform player = Thing(_rig.Context.Self.forward * 3f);
            _walls.Blocked.Add(player);

            Look(sensor);

            Assert.IsNull(Seen());
        }

        // ---------- Zone ----------

        [Test]
        public void Zone_SeesWhatStandsInsideItsOwnZone_WhereverTheAgentIs()
        {
            const string KIND = "Watch Zone";
            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.hideFlags = HideFlags.HideAndDontSave;
            _created.Add(zone);
            zone.transform.position = new Vector3(50f, 0f, 0f);
            zone.transform.localScale = new Vector3(4f, 1f, 4f);
            var marker = zone.AddComponent<AIObject>();       // the zone says whose it is (step 90: no binding)
            marker.Kind = KIND;
            marker.Owners.Add(_rig.Agent);
            _rig.UseObjects().Register(marker);
            var sensor = new ZoneSensorTask { StoreIn = SEEN, Zone = KIND, LoseMargin = 1f, Interval = 0f };
            Transform player = Thing(new Vector3(50f, 0f, 3f));

            Look(sensor);
            Assert.IsNull(Seen(), "Outside the zone");

            player.position = new Vector3(51f, 0f, 1f);
            Look(sensor);
            Assert.AreSame(player, Seen(), "Inside the zone, far from the agent");

            player.position = new Vector3(52.5f, 0f, 0f);
            Look(sensor);
            Assert.AreSame(player, Seen(), "Kept up to Lose Margin outside the edge");
        }

        [Test]
        public void Zone_NoZoneOfItsOwn_SeesNothing()
        {
            GameObject zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            zone.hideFlags = HideFlags.HideAndDontSave;
            _created.Add(zone);
            var marker = zone.AddComponent<AIObject>();
            marker.Kind = "Somebody's Zone";
            marker.Owners.Add(new GraphRig(trace: false).Agent);
            _rig.UseObjects().Register(marker);
            Thing(Vector3.zero);

            Look(new ZoneSensorTask { StoreIn = SEEN, Zone = "Nowhere", Interval = 0f });
            Assert.IsNull(Seen(), "No zone of the kind");

            Look(new ZoneSensorTask { StoreIn = SEEN, Zone = marker.Kind, Interval = 0f });
            Assert.IsNull(Seen(), "A zone of the kind, but another's: not its own");
        }

        // ---------- Detection ----------

        [Test]
        public void Detection_AnyShapeSees_TheObjectIsHeld_NoneSees_ItIsLost()
        {
            var detection = new DetectionTask(
                new RadiusSensorTask { Radius = 2f, LoseRadius = 2f },
                new ConeSensorTask { Range = 8f, HalfAngle = 30f, LoseRange = 8f }) { StoreIn = SEEN, Interval = 0f };
            Vector3 forward = _rig.Context.Self.forward;
            Transform player = Thing(-forward * 5f);

            Look(detection);
            Assert.IsNull(Seen(), "Too far for the radius, behind the cone");

            player.position = forward * 6f;
            Look(detection);
            Assert.AreSame(player, Seen(), "The cone sees it");

            player.position = -forward * 9f;
            Look(detection);
            Assert.IsNull(Seen(), "None of the shapes sees it any more");
        }

        [Test]
        public void Detection_LooksEveryIntervalGiveOrTakeAFifth_FromTheAgentsOwnRandom()
        {
            const float INTERVAL = 1f;
            var detection = new DetectionTask(new RadiusSensorTask { Radius = 10f }) { StoreIn = SEEN, Interval = INTERVAL };
            Look(detection);
            var gaps = new List<float>();
            float last = 0f;
            Transform player = null;
            for (float t = DT; t < 20f && gaps.Count < 6; t += DT)
            {
                if (player == null)
                    player = Thing(new Vector3(1f, 0f, 0f));

                Look(detection);
                if (Seen() != null)
                {
                    gaps.Add(t - last);
                    last = t;
                    _rig.Context.Blackboard.Set(SEEN, (Transform)null);     // so the next look shows up too
                    _finder.Objects.Clear();
                    player = null;
                }
            }

            Assert.GreaterOrEqual(gaps.Count, 3);
            foreach (float gap in gaps)
                Assert.That(gap, Is.InRange(0.8f * INTERVAL - DT, 1.2f * INTERVAL + DT), "Agents do not all look in the same frame");
        }

        // ---------- helpers ----------

        private Transform Thing(Vector3 position)
        {
            var go = new GameObject("Player") { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = position;
            _finder.Objects.Add(go.transform);
            return go.transform;
        }

        private void Look(SensorTask sensor) => sensor.Tick(_rig.Context, DT);

        private void Steps(SensorTask sensor, float seconds)
        {
            for (float t = 0f; t < seconds; t += DT)
            {
                _rig.Tick(DT);                              // the agent's clock: the grace of a lost line counts on it
                sensor.Tick(_rig.Context, DT);
            }
        }

        private Transform Seen() => _rig.Context.Blackboard.Get<Transform>(SEEN);

        // The objects of a kind, as the scene would give them: every one within the radius
        private sealed class ListFinder : IObjectFinder
        {
            public readonly List<Transform> Objects = new();

            public Transform FindNearest(string kind, Vector3 center, float radius, Transform self)
            {
                Transform nearest = null;
                foreach (Transform found in Objects)
                {
                    if (found != null && (found.position - center).sqrMagnitude <= radius * radius &&
                        (nearest == null || (found.position - center).sqrMagnitude < (nearest.position - center).sqrMagnitude))
                        nearest = found;
                }

                return nearest;
            }

            public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
            {
                results.Clear();
                foreach (Transform found in Objects)
                {
                    if (found != null && (found.position - center).sqrMagnitude <= radius * radius)
                        results.Add(found);
                }
            }
        }

        // The walls: a line to one of these objects is blocked (the eyes' height does not matter)
        private sealed class Walls : ISightProbe
        {
            public readonly List<Transform> Blocked = new();

            public bool HasLineOfSight(Vector3 from, Vector3 to)
            {
                foreach (Transform blocked in Blocked)
                {
                    Vector3 offset = blocked.position - to;
                    offset.y = 0f;
                    if (offset.sqrMagnitude < 1e-4f)
                        return false;
                }

                return true;
            }
        }
    }
}
