using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// The shapes of Detection as the lab wires them (step-90 recheck): Radius (Line Of Sight) keeps its grace per target
    /// inside Detection too, Calls For Help keeps to its own Within beside a shape that gathers from farther, a Cone with
    /// a line of sight aims level from the eyes as Radius (Line Of Sight) does and measures its angle along the facing of
    /// a side-view body; and the corners of every shape — the reach of Detection, a second kind, a Lose Radius under the
    /// Radius, the edge of a cone, a zone of its own, a hitbox — the attack place given back with a lost target, and a
    /// look that allocates nothing.
    /// </summary>
    public sealed class SensesDetectionTests
    {
        private const string SEEN = "Target";
        private const float DT = 0.05f;

        private readonly Things _things = new();
        private GraphRig _rig;
        private KindFinder _finder;
        private WallProbe _walls;
        private WorldObjects _objects;

        [SetUp]
        public void SetUp()
        {
            _objects = null;
            _rig = new GraphRig(trace: false);
            _finder = new KindFinder();
            _walls = new WallProbe();
            _rig.Context.Objects = _finder;
            _rig.Context.Sight = _walls;
        }

        [TearDown]
        public void TearDown()
        {
            _things.DestroyAll();
            GraphRig.DestroyAll();
        }

        // ---------- Radius (Line Of Sight) inside Detection ----------

        [Test]
        public void DetectionTask_SightRadiusShape_AnotherObjectOfTheKindInSight_DoesNotKeepAHeldOneBehindAWall()
        {
            const float GRACE = 1f;
            var detection = new DetectionTask(new SightRadiusSensorTask { Radius = 10f, LoseRadius = 10f, LoseSightAfter = GRACE })
                { StoreIn = SEEN, Interval = 0f };
            Transform held = Thing(new Vector3(3f, 0f, 0f));
            Look(detection);
            Assume.That(Seen(), Is.SameAs(held), "Set-up: the near one is held");

            // In clear sight all the while, farther — and listed first, so every look sees its clear line before it
            // comes to the held one (the scene lists objects in any order)
            Transform other = Thing(new Vector3(-6f, 0f, 0f));
            _finder.Objects.Remove(other);
            _finder.Objects.Insert(0, other);
            _walls.Blocked.Add(held);
            Steps(detection, GRACE - 0.2f);
            Assert.AreSame(held, Seen(), "Within its own grace: kept");

            Steps(detection, 0.4f);
            Assert.AreSame(other, Seen(),
                "Its own line has been blocked longer than Lose Sight After: lost, and the one in sight is taken " +
                "(handbook §9.4: a stamp per target — another object's clear line does not keep it)");
        }

        [Test]
        public void SightRadius_TheOneTakenWhenTheHeldOneIsLost_GetsItsOwnGrace_NotWhatIsLeftOfTheLostOnes()
        {
            const float GRACE = 1f;
            var detection = new DetectionTask(new SightRadiusSensorTask { Radius = 10f, LoseRadius = 10f, LoseSightAfter = GRACE })
                { StoreIn = SEEN, Interval = 0f };
            Transform first = Thing(new Vector3(3f, 0f, 0f));
            Look(detection);
            Transform second = Thing(new Vector3(-6f, 0f, 0f));
            _walls.Blocked.Add(first);
            for (float t = 0f; t < GRACE + 0.2f && Seen() != second; t += DT)
                Steps(detection, DT);
            Assume.That(Seen(), Is.SameAs(second), "Set-up: the first is lost after its grace, the second taken");

            _walls.Blocked.Add(second);                             // the newly taken one ducks behind a wall at once
            Steps(detection, GRACE - 0.2f);
            Assert.AreSame(second, Seen(),
                "It was in clear sight when it was taken: its grace counts from then, not from the lost one's last clear line");
            Steps(detection, 0.4f);
            Assert.IsNull(Seen(), "...and runs out like any other");
        }

        // ---------- Calls For Help beside a farther shape ----------

        [Test]
        public void CallsForHelpSensor_AShapeBesideReachesFarther_ItStillKeepsToItsOwnWithin()
        {
            var bus = new SignalBus();
            GraphRig caller = Listening(bus, Vector3.zero);
            GraphRig ally = Listening(bus, new Vector3(4f, 0f, 0f));
            var heard = new CallsForHelpSensorTask { Within = 12f, Remember = 8f };
            float within = heard.Within.Get(null);
            var detection = new DetectionTask(new RadiusSensorTask { Radius = 2f, LoseRadius = 40f }, heard)
                { StoreIn = "Kin", Interval = 0f };
            Assume.That(detection.Reach(ally.Context), Is.EqualTo(40f), "Detection gathers as far as its farthest shape");
            Transform enemy = Thing(ally.Actuator.Position + Vector3.left * 20f, "Player");
            ally.Context.Objects = new KindFinder(enemy);
            caller.Context.Blackboard.Set("Foe", enemy);
            new CallForHelpTask { Target = "Foe" }.Tick(caller.Context, DT);
            Assume.That(ally.Context.HasSignal(CallForHelpTask.SIGNAL), "The ally heard the call");

            ally.Context.Advance(DT);
            detection.Tick(ally.Context, DT);
            Assert.IsNull(detection.SeenBy(ally.Context),
                $"20 m away: farther than Within ({within} m) and than Radius — not its business, whatever Detection gathers");

            enemy.position = ally.Actuator.Position + Vector3.left * (within - 1f);
            ally.Context.Advance(DT);
            detection.Tick(ally.Context, DT);
            Assert.AreSame(enemy, detection.SeenBy(ally.Context), "Within Within, the call fresh: seen");
        }

        // ---------- Cone with a line of sight ----------

        [Test]
        public void Cone_WithLineOfSight_ATargetStandingOnTheGround_IsSeenLikeSightRadiusSeesIt()
        {
            _rig.Context.Sight = new GroundPlane();
            Transform target = Thing(new Vector3(3f, -0.01f, 0f));     // its pivot a centimetre under the ground
            var cone = new ConeSensorTask { StoreIn = "Cone", Range = 8f, HalfAngle = 180f, LineOfSight = true, Interval = 0f };
            var sight = new SightRadiusSensorTask { StoreIn = "Sight", Radius = 8f, Interval = 0f };
            Assume.That(_rig.Context.EyePosition.y, Is.EqualTo(1.6f * 0.9f).Within(1e-4f), "The eyes: 0.9 of the body's height");

            cone.Tick(_rig.Context, DT);
            sight.Tick(_rig.Context, DT);

            Assert.AreSame(target, sight.SeenBy(_rig.Context), "A level line from the eyes stays above the ground");
            Assert.AreSame(target, cone.SeenBy(_rig.Context), "The cone's line too: not a ray into the ground at its feet");
        }

        // ---------- a side-view body ----------

        [Test]
        public void Cone_ASideViewBody_SeesAlongItsFacing_NotStraightAboveIt()
        {
            AICharacter me = SideViewCharacter(Vector3.right);
            var finder = new KindFinder();
            me.Context.Objects = finder;
            Transform ahead = Thing(new Vector3(5f, 0f, 0f));
            Transform above = Thing(new Vector3(0f, 6f, 0f));
            var cone = new ConeSensorTask { StoreIn = SEEN, Range = 8f, HalfAngle = 45f, LoseRange = 8f, Interval = 0f };

            finder.Objects.Add(above);
            cone.Tick(me.Context, DT);
            Assert.IsNull(cone.SeenBy(me.Context), "Straight above a body that looks along +X: 90° off its facing");

            finder.Objects.Clear();
            finder.Objects.Add(ahead);
            cone.Tick(me.Context, DT);
            Assert.AreSame(ahead, cone.SeenBy(me.Context), "Ahead of where the side-view body looks: seen");
        }

        // ---------- Detection and its shapes ----------

        [Test]
        public void DetectionTask_LosesItsTarget_GivesBackTheAttackPlaceOnIt()
        {
            var world = new AIWorld();
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            var finder = new KindFinder();
            rig.Context.Objects = finder;
            Transform target = _things.Make("Foe", new Vector3(3f, 0f, 0f));
            target.gameObject.AddComponent<AttackSlotCapacity>().Slots = 1;
            finder.Objects.Add(target);
            var radius = new RadiusSensorTask { Radius = 5f, LoseRadius = 7f };
            var detection = new DetectionTask(radius) { StoreIn = SEEN, Interval = 0f };

            detection.Tick(rig.Context, DT);
            Assume.That(detection.SeenBy(rig.Context), Is.SameAs(target));
            Take(rig, world);
            Assume.That(world.Roster.FindEngagement(target).Tokens.Holds(rig.Agent, AttackSlots.PoolOf("")), "Set-up: a place");
            Assert.AreEqual("gives the attack place back when the target is lost", detection.Requirement,
                "What it does by itself is said on its node");

            target.position = new Vector3(radius.LoseRadius.Get(null) + 1f, 0f, 0f);
            detection.Tick(rig.Context, DT);

            Assert.IsNull(detection.SeenBy(rig.Context), "Beyond Lose Radius: lost");
            Assert.IsFalse(world.Roster.FindEngagement(target)?.Tokens.Holds(rig.Agent, AttackSlots.PoolOf("")) ?? false,
                "...and the place to attack it from is given back (a place for what is not seen is held for nothing)");
        }

        [Test]
        public void SensorTask_LosesAndFindsItsTarget_AllocatesNothing()
        {
            var world = new AIWorld();
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            var finder = new KindFinder();
            rig.Context.Objects = finder;
            Transform target = _things.Make("Foe", new Vector3(3f, 0f, 0f));
            target.gameObject.AddComponent<AttackSlotCapacity>().Slots = 1;
            finder.Objects.Add(target);
            var detection = new DetectionTask(new RadiusSensorTask { Radius = 5f, LoseRadius = 7f }) { StoreIn = SEEN, Interval = 0f };
            detection.Tick(rig.Context, DT);
            Take(rig, world);
            var near = new Vector3(3f, 0f, 0f);
            var far = new Vector3(8f, 0f, 0f);

            for (int i = 0; i < 20; i++)
            {
                target.position = i % 2 == 0 ? far : near;
                detection.Tick(rig.Context, DT);
            }

            // Unity's own GC.Alloc counter (GC.GetAllocatedBytesForCurrentThread says 0 in the editor, whatever is made)
            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    target.position = i % 2 == 0 ? far : near;
                    detection.Tick(rig.Context, DT);
                }
            }, Is.Not.AllocatingGCMemory(), "Losing a target (and giving its place back) and finding it again allocates nothing");
            Assert.AreSame(target, detection.SeenBy(rig.Context), "It was found again on the last look");
        }

        [Test]
        public void Detection_AGraphWithEveryShape_StepsWithoutAllocating()
        {
            var g = new BrainGraphBuilder();
            g.Variable(SEEN, VariableType.Transform);
            g.Sensor(new DetectionTask(
                new RadiusSensorTask { Radius = 2f, LoseRadius = 3f },
                new SightRadiusSensorTask { Radius = 6f, LoseRadius = 8f, LoseSightAfter = 0.3f },
                new ConeSensorTask { Range = 8f, HalfAngle = 60f, LineOfSight = true },
                new HitBySensorTask(),
                new CallsForHelpSensorTask()) { StoreIn = SEEN, Interval = 0f, AlsoTag = "Blue" });
            g.BrainTask.Target = SEEN;
            g.Chain(g.Always(), new WaitTask(100f));
            _rig.Agent.SetSignals(new SignalBus());                 // a bus of its own: no runner of the scene is made
            _rig.SetGraph(g.Build());
            Transform player = Thing(new Vector3(4f, 0f, 0f));
            Thing(new Vector3(-5f, 0f, 1f));

            void Run(int steps)
            {
                for (int i = 0; i < steps; i++)
                {
                    player.position = new Vector3(4f + 6f * Mathf.Sin(i * 0.05f), 0f, 0f);    // in and out of sight
                    if (i % 40 == 0)
                        _walls.Blocked.Add(player);
                    else if (i % 40 == 20)
                        _walls.Blocked.Clear();
                    _rig.Step(0.02f);
                }
            }

            Run(200);

            // Unity's own GC.Alloc counter (GC.GetAllocatedBytesForCurrentThread says 0 in the editor, whatever is made)
            Assert.That(() => Run(400), Is.Not.AllocatingGCMemory(),
                "A Detection of every shape looks, holds, loses and takes again without allocating");
        }

        [Test]
        public void DetectionTask_ReachIsTheLargestOfItsShapes_ListensOnlyWithCallsForHelp_EmptySeesNothing()
        {
            var detection = new DetectionTask(new RadiusSensorTask { Radius = 5f, LoseRadius = 7f },
                new ConeSensorTask { Range = 8f, LoseRange = 10f });
            Assert.AreEqual(10f, detection.Reach(_rig.Context), "The farthest a shape keeps: the cone's Lose Range");
            Assert.IsFalse(detection.Listens, "No Calls For Help wired in: deaf to calls");

            detection.Sensors.Add(new HitBySensorTask { Within = 30f });
            Assert.AreEqual(30f, detection.Reach(_rig.Context), "A blow comes from as far as Within");
            detection.Sensors.Add(new ZoneSensorTask());
            Assert.AreEqual(float.MaxValue, detection.Reach(_rig.Context), "A zone is watched wherever it is");
            detection.Sensors.Add(new CallsForHelpSensorTask());
            Assert.IsTrue(detection.Listens, "Calls For Help wired in: it hears calls");

            var empty = new DetectionTask { StoreIn = SEEN, Interval = 0f };
            Thing(new Vector3(1f, 0f, 0f));
            Assert.AreEqual(0f, empty.Reach(_rig.Context), "No shape: it reaches nowhere");
            empty.Tick(_rig.Context, DT);
            Assert.IsNull(Seen(), "No shape: nothing is seen");

            empty.Sensors.Add(null);                                // a shape whose node was taken away
            Assert.DoesNotThrow(() => empty.Tick(_rig.Context, DT));
            Assert.IsNull(Seen());
        }

        [Test]
        public void Sensor_AlsoTagUntaggedOrTheSameAsTag_IsNoSecondKind()
        {
            _finder.ByTag = true;
            Transform player = Thing(new Vector3(2f, 0f, 0f), "Player");
            foreach ((string also, int looks) in new[] { ("Untagged", 1), ("Player", 1), ("Respawn", 2) })
            {
                var sensor = new DetectionTask(new RadiusSensorTask { Radius = 10f }) { Tag = "Player", AlsoTag = also, StoreIn = SEEN, Interval = 0f };
                _finder.Calls.Clear();
                _rig.Context.Blackboard.Set(SEEN, (Transform)null);

                sensor.Tick(_rig.Context, DT);

                int calls = 0;
                foreach (int count in _finder.Calls.Values)
                    calls += count;
                Assert.AreEqual(looks, calls, $"Also Tag '{also}': kinds asked for in one look");
                Assert.AreSame(player, Seen(), $"Also Tag '{also}': the player is seen once, whatever else is asked");
            }
        }

        [Test]
        public void Radius_LoseRadiusSmallerThanRadius_KeepsUpToRadius()
        {
            var sensor = new RadiusSensorTask { StoreIn = SEEN, Radius = 5f, LoseRadius = 3f, Interval = 0f };
            Transform player = Thing(new Vector3(4.9f, 0f, 0f));

            Look(sensor);
            Assert.AreSame(player, Seen(), "Within Radius: noticed");
            Look(sensor);
            Assert.AreSame(player, Seen(), "Kept: a Lose Radius under Radius means Radius (it is not dropped at 3 m)");

            player.position = new Vector3(5.1f, 0f, 0f);
            Look(sensor);
            Assert.IsNull(Seen(), "Beyond Radius: lost");
        }

        [Test]
        public void Cone_JustInsideAndOutsideHalfAngle_AndOnTopOfTheAgent()
        {
            const float HALF = 45f;
            _rig.Actuator.LooksIn3D = true;                         // a body in 3D looking along +Z
            _rig.Actuator.Face(Vector3.forward);
            var sensor = new ConeSensorTask { StoreIn = SEEN, Range = 8f, HalfAngle = HALF, Interval = 0f };
            Transform player = Thing(At(HALF - 1f, 5f));

            Look(sensor);
            Assert.AreSame(player, Seen(), "A degree inside the half-angle: seen");

            _rig.Context.Blackboard.Set(SEEN, (Transform)null);
            player.position = At(HALF + 1f, 5f);
            Look(sensor);
            Assert.IsNull(Seen(), "A degree outside: not seen");

            player.position = _rig.Actuator.Position;
            Look(sensor);
            Assert.AreSame(player, Seen(), "On top of the agent: no way to it to measure, so no angle to fail");
        }

        [Test]
        public void Zone_AnAIObjectWithACircleOfItsOwn_IsWatched_KeptLoseMarginOutside()
        {
            const string KIND = "Watch";
            AIObject zone = Zone(KIND, new Vector3(50f, 0f, 0f), 2f);
            var sensor = new ZoneSensorTask { StoreIn = SEEN, Zone = KIND, LoseMargin = 1f, Interval = 0f };
            Transform player = Thing(new Vector3(51.9f, 0f, 0f));
            Assume.That(zone.HasZone, "A circle of its own, no renderer");

            Look(sensor);
            Assert.AreSame(player, Seen(), "1.9 m from the middle of a 2 m circle: inside");
            player.position = new Vector3(52.5f, 0f, 0f);
            Look(sensor);
            Assert.AreSame(player, Seen(), "2.5 m: outside, but within Lose Margin of the edge — kept");
            player.position = new Vector3(53.1f, 0f, 0f);
            Look(sensor);
            Assert.IsNull(Seen(), "3.1 m: beyond the margin — lost");
            player.position = new Vector3(52.5f, 0f, 0f);
            Look(sensor);
            Assert.IsNull(Seen(), "2.5 m again: not held now, so no margin — not noticed");
        }

        [Test]
        public void Zone_TwoZonesOfTheKindOwned_TheNearestIsWatched()
        {
            const string KIND = "Watch";
            Zone(KIND, new Vector3(10f, 0f, 0f), 2f);
            Zone(KIND, new Vector3(40f, 0f, 0f), 2f);
            var sensor = new ZoneSensorTask { StoreIn = SEEN, Zone = KIND, Interval = 0f };
            Transform player = Thing(new Vector3(40.5f, 0f, 0f));

            Look(sensor);
            Assert.IsNull(Seen(), "Inside the far zone: the agent watches the zone nearest to it");

            player.position = new Vector3(10.5f, 0f, 0f);
            Look(sensor);
            Assert.AreSame(player, Seen(), "Inside the near zone: seen");

            _rig.Context.Blackboard.Set(SEEN, (Transform)null);
            _rig.Actuator.Position = new Vector3(35f, 0f, 0f);
            player.position = new Vector3(40.5f, 0f, 0f);
            Look(sensor);
            Assert.AreSame(player, Seen(), "The agent went over: the far zone is the nearest now");
        }

        [Test]
        public void HitBy_AnAttackerThatIsAChildOfTheCandidate_SeesTheCandidate()
        {
            Transform enemy = Thing(new Vector3(4f, 0f, 0f));
            Transform hitbox = _things.Make("Hitbox", enemy.position);
            hitbox.SetParent(enemy);
            _rig.Agent.TakeHit(new HitInfo(hitbox, 1f));
            var detection = new DetectionTask(new HitBySensorTask { Remember = 5f }) { StoreIn = SEEN, Interval = 0f };

            Look(detection);

            Assert.AreSame(enemy, Seen(), "Struck by its hitbox: the one the scene lists is seen");
        }

        // ---------- helpers ----------

        private Vector3 At(float degreesFromForward, float distance) =>
            _rig.Actuator.Position + Quaternion.AngleAxis(degreesFromForward, Vector3.up) * Vector3.forward * distance;

        // An AI Object of the kind with a circle of its own, the agent's
        private AIObject Zone(string kind, Vector3 at, float radius)
        {
            Transform holder = _things.Make($"{kind} Zone", at);
            var zone = holder.gameObject.AddComponent<AIObject>();
            zone.Kind = kind;
            zone.ZoneRadius = radius;
            zone.Owners.Add(_rig.Agent);
            _objects ??= _rig.UseObjects();                         // a registry of the test's own, not the scene's
            _objects.Register(zone);
            return zone;
        }

        // The place given: asked until the group answers
        private static void Take(GraphRig rig, AIWorld world)
        {
            rig.Context.Blackboard.Set("Slot Target", rig.Context.Blackboard.Get<Transform>(SEEN));
            var take = new TakeAttackSlotTask { Target = "Slot Target" };
            TaskStatus answer = TaskStatus.Running;
            for (int i = 0; i < 100 && answer == TaskStatus.Running; i++)
            {
                answer = take.Tick(rig.Context, DT);
                world.BeginStep(DT);
            }

            Assume.That(answer, Is.EqualTo(TaskStatus.Success), "Set-up: the place is given");
        }

        private Transform Thing(Vector3 at, string tag = null)
        {
            Transform thing = _things.Make("Player", at, tag);
            _finder.Objects.Add(thing);
            return thing;
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

        // An awake agent on the bus: it hears the others and shouts to them
        private static GraphRig Listening(SignalBus bus, Vector3 at)
        {
            var rig = new GraphRig(trace: false);
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.SetSignals(bus);
            return rig;
        }

        // A character with the module's side-view body (a SimpleBody in the XY plane), looking along `facing`
        private AICharacter SideViewCharacter(Vector3 facing)
        {
            var go = _things.Keep(new GameObject("Side View") { hideFlags = HideFlags.HideAndDontSave });
            go.SetActive(false);
            var character = go.AddComponent<AICharacter>();
            character.Simulation = AICharacter.SimulationMode.Manual;
            character.Body = new SimpleBody { PlanarXY = true };
            character.Initialize();
            character.Context.Actuator.Face(facing);
            return character;
        }
    }
}
