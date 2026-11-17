using System.Collections.Generic;
using System.Linq;
using AIKit.NavMeshNavigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AIKit.Tests
{
    public sealed class NavMeshBodyTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;      // a NavMeshAgent without a baked mesh complains

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            LogAssert.ignoreFailingMessages = false;
        }

        private AICharacter Make(NavMeshBody body)
        {
            _go = new GameObject("Character") { hideFlags = HideFlags.HideAndDontSave };
            _go.SetActive(false);
            _go.AddComponent<NavMeshAgent>();
            AICharacter character = _go.AddComponent<AICharacter>();
            character.Body = body;
            character.Initialize();
            return character;
        }

        [Test]
        public void AICharacter_NavMeshBodyChosen_PartsComeFromTheBody()
        {
            var body = new NavMeshBody();
            AICharacter character = Make(body);

            Assert.That(character.Context.Actuator, Is.SameAs(body.Mover));
            Assert.That(character.Context.Navigation, Is.SameAs(body.Pathing));
            Assert.That(character.Context.Sight, Is.SameAs(body.Eyes));
            Assert.That(character.Context.Scanner, Is.SameAs(body.Eyes), "the eyes scan for a Senses sensor's targets");
            Assert.That(character.Context.Perception, Is.Null, "no Senses sensor in its graph: no senses (step 90, C21)");
        }

        [Test]
        public void NavMeshBody_ReportsWhereItLooks_TheSensesConesFollowItInThreeD()
        {
            var body = new NavMeshBody();
            AICharacter character = Make(body);
            _go.transform.rotation = Quaternion.LookRotation(Vector3.left);

            AgentBodyInfo info = character.Context.Actuator.Body;

            Assert.That(Vector3.Angle(info.Forward, Vector3.left), Is.LessThan(0.01f), "its 3D forward (was the world's +X)");
            Assert.That(Vector3.Angle(AgentPerception.LookOf(info, Vector3.up), Vector3.left), Is.LessThan(0.01f),
                "the senses look where the body looks");
        }

        [Test]
        public void NavMeshBody_TheInspectorsEdgeOptions_TheClipsNumbersAreItsHiddenData()
        {
            // Step 90 (C20): of the ~20 numbers of climbing and jumping only the options are seen; the numbers are the
            // clips' — measured into the body, kept hidden, shown as one line
            var shown = new List<string>();
            foreach (System.Reflection.FieldInfo field in typeof(NavMeshBody).GetFields(
                         System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic))
            {
                bool serialized = field.IsPublic && !field.IsNotSerialized || field.IsDefined(typeof(SerializeField), false);
                if (serialized && !field.IsDefined(typeof(HideInInspector), false))
                    shown.Add(field.Name);
            }

            foreach (string gone in new[]
                     {
                         "ClimbTime", "ClimbSpeed", "ClimbRise", "ClimbAdvance", "ClimbReach", "ClimbFromWall", "ClimbStepSpeed",
                         "JumpTakeOff", "JumpLanding", "JumpHop", "JumpFromEdge", "FallSpeedUp", "ArcHeight", "JumpSpeed",
                         "MinJumpDuration", "ClimbCheckInterval", "ClimbSpacing", "AllyLayers"
                     })
                Assert.That(shown, Has.No.Member(gone), $"{gone} is no setting of the body any more");

            Assert.That(shown, Has.Member("ClimbsWalls"));
            Assert.That(shown, Has.Member("JumpsOverLinks"));
            Assert.That(shown, Has.Member("ClimbsDownByJumping"), "the one option of the way down");
            Assert.That(new NavMeshBody().ClimbsDownByJumping, Is.True, "down by a jump unless said otherwise");
            Assert.That(new NavMeshBody().Traversal.IsMeasured, Is.False, "a new body is not measured: the plain way");
        }

        [Test]
        public void NavMeshBody_NoJumps_JumpAreaIsLeftOut()
        {
            var body = new NavMeshBody { JumpsOverLinks = false };
            Make(body);
            Assert.That(_go.GetComponent<NavMeshAgent>().areaMask & (1 << NavMeshLinkJumper.JUMP_AREA), Is.Zero);
            Assert.That(body.Jumper, Is.Null);
        }

        [Test]
        public void NavMeshBody_Jumps_JumpAreaIsWalkable()
        {
            var body = new NavMeshBody { JumpsOverLinks = true };
            Make(body);
            Assert.That(_go.GetComponent<NavMeshAgent>().areaMask & (1 << NavMeshLinkJumper.JUMP_AREA), Is.Not.Zero);
            Assert.That(body.Jumper, Is.Not.Null);
        }

        [Test]
        public void NavMeshBody_Acceleration_GoesToTheAgent()
        {
            var body = new NavMeshBody { Acceleration = 5f };
            Make(body);
            Assert.That(_go.GetComponent<NavMeshAgent>().acceleration, Is.EqualTo(5f));
        }

        [Test]
        public void NavMeshBody_RampDistance_TheAccelerationFitsTheSpeed_TheRampIsAlwaysThatLong()
        {
            var body = new NavMeshBody { RampDistance = 0.5f, Acceleration = 8f };
            Make(body);
            NavMeshAgent agent = _go.GetComponent<NavMeshAgent>();
            float fast = agent.acceleration;

            Assert.That(Ramp(agent), Is.EqualTo(0.5f).Within(1e-3f), "At its full speed it is half a metre");

            body.Pathing.Pace = 0.4f;                           // a walking pace: the same distance, taken gently

            Assert.That(Ramp(agent), Is.EqualTo(0.5f).Within(1e-3f), "And at a walking pace it is half a metre too");
            Assert.That(agent.acceleration, Is.LessThan(fast), "A slow step starts gently, a fast one bursts off");
        }

        [Test]
        public void NavMeshBody_RampDistance_AWalkingPaceKeepsItsGentleStart_NoCopyOnTheBlackboardWritesItBack()
        {
            // A pace works the acceleration out anew. When the body's numbers were copied onto the blackboard (step 34),
            // the copy still had the full speed's one and wrote it back the next step — a patrol walking at 1.6 m/s burst
            // off at a run's 13 m/s². Step 90: no copy at all — the body is the one owner, its status reads it live, and
            // the graph changes it only through Set Parameter (SetParameterTests)
            var body = new NavMeshBody { RampDistance = 0.5f, Acceleration = 8f };
            AICharacter character = Make(body);
            character.SetGraph(new BrainGraphBuilder().Build());             // a Brain with nothing to decide: it stands
            NavMeshAgent agent = _go.GetComponent<NavMeshAgent>();
            character.Step(DT);

            body.Pathing.Pace = 0.4f;
            float gentle = agent.acceleration;
            character.Step(DT);
            character.Step(DT);

            Assert.That(agent.acceleration, Is.EqualTo(gentle).Within(1e-4f), "The walk keeps its gentle start");
            Assert.That(Ramp(agent), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.IsFalse(character.Blackboard.Has(NavMeshMover.ACCELERATION), "No copy of the body's numbers on the blackboard");
            Assert.IsTrue(character.TryGetStat(CharacterStat.Acceleration, out float shown));
            Assert.That(shown, Is.EqualTo(gentle).Within(1e-4f), "The status shows the body's acceleration as it is now");
        }

        /// <summary>The metres a body needs to get up to its speed: what the feet of a start clip cover.</summary>
        private static float Ramp(NavMeshAgent agent) => agent.speed * agent.speed / (2f * agent.acceleration);

        [Test]
        public void NavMeshBody_GroundOffset_LowersTheBodyBelowTheMesh()
        {
            var body = new NavMeshBody { GroundOffset = -0.08f };
            Make(body);
            Assert.That(_go.GetComponent<NavMeshAgent>().baseOffset, Is.EqualTo(-0.08f));
        }

        [Test]
        public void NavMeshBody_OwnAvoidancePriority_EveryBodyGetsOneOfItsOwn_TheSameOneEveryRun()
        {
            var body = new NavMeshBody();
            Make(body);
            int given = _go.GetComponent<NavMeshAgent>().avoidancePriority;

            Assert.That(given, Is.EqualTo(NavMeshBody.PriorityFor("Character")), "from the name: the same one every run");
            Assert.That(given, Is.InRange(30, 69), "the middle of the range: the game may still set somebody above all");

            var crowd = new System.Collections.Generic.HashSet<int>();
            for (int i = 1; i <= 10; i++)
                crowd.Add(NavMeshBody.PriorityFor("Skeleton " + i));

            Assert.That(crowd.Count, Is.GreaterThanOrEqualTo(8),
                "a crowd of ten is not of one mind about who gives way: they would step aside together and shiver");
        }

        [Test]
        public void NavMeshBody_OwnAvoidancePriorityOff_WhatTheAgentWasSetTo_IsKept()
        {
            _go = new GameObject("Set by hand") { hideFlags = HideFlags.HideAndDontSave };
            _go.SetActive(false);
            NavMeshAgent agent = _go.AddComponent<NavMeshAgent>();
            agent.avoidancePriority = 7;                        // the game says this one pushes through
            AICharacter character = _go.AddComponent<AICharacter>();
            character.Body = new NavMeshBody { OwnAvoidancePriority = false };

            character.Initialize();

            Assert.That(agent.avoidancePriority, Is.EqualTo(7));
        }

        // ---------- the glide: how the body speeds up and brakes (NavMeshAgent does not move in Edit Mode) ----------

        private const float DT = 0.05f;
        private const float ACCELERATION = 6f;
        private const float SPEED = 4f;

        private static readonly Vector3 Ahead = Vector3.right * SPEED;

        [Test]
        public void Glide_AHandHeldStep_SpeedsUpWithTheAcceleration_NotAtOnce()
        {
            var glide = new BodyGlide();

            Vector3 first = glide.Step(Ahead, false, Vector3.zero, ACCELERATION, DT, out _);

            Assert.That(first.magnitude / DT, Is.EqualTo(ACCELERATION * DT).Within(1e-4f), "One step in: what the acceleration allows");
            for (int i = 0; i < Mathf.CeilToInt(SPEED / ACCELERATION / DT); i++)
                glide.Step(Ahead, false, Vector3.zero, ACCELERATION, DT, out _);
            Assert.That(glide.Velocity.magnitude, Is.EqualTo(SPEED).Within(1e-4f), "Then its full speed, no more");
        }

        [Test]
        public void Glide_NothingAsked_BrakesOverTheDistanceItsAccelerationNeeds_ThenStands()
        {
            var glide = new BodyGlide();
            for (int i = 0; i < 40; i++)
                glide.Step(Ahead, false, Vector3.zero, ACCELERATION, DT, out _);

            float slid = 0f;
            for (int i = 0; i < Mathf.CeilToInt(SPEED / ACCELERATION / DT) + 4; i++)
                slid += glide.Step(null, false, Vector3.zero, ACCELERATION, DT, out _).magnitude;

            Assert.That(slid, Is.EqualTo(SPEED * SPEED / (2f * ACCELERATION)).Within(0.2f), "Brakes, not on the spot");
            Assert.That(glide.Velocity, Is.EqualTo(Vector3.zero), "Then stands");
        }

        [Test]
        public void Glide_APathEnds_ItsSpeedGlidesOn_APathBegins_TakesTheGlidesSpeed()
        {
            var glide = new BodyGlide();
            Vector3 onPath = glide.Step(null, true, Ahead, ACCELERATION, DT, out bool handOver);
            Assert.AreEqual(Vector3.zero, onPath, "On a path the agent moves itself");
            Assert.IsFalse(handOver, "Nothing to hand over from a standstill");

            Vector3 after = glide.Step(null, false, Vector3.zero, ACCELERATION, DT, out _);
            Assert.That(after.magnitude / DT, Is.EqualTo(SPEED - ACCELERATION * DT).Within(1e-4f),
                "The path cleared: the body goes on at its speed and brakes");

            glide.Step(null, true, Vector3.zero, ACCELERATION, DT, out handOver);
            Assert.IsTrue(handOver, "A new path starts from the speed the body has, not from nothing");
        }

        [Test]
        public void Glide_AStepTurnsTheOtherWay_TheBodyTurnsItsSpeed_NotBrakingThroughAStandstill()
        {
            // A wary sidestep that turns back the other way (Square Off). Braking to nothing and speeding up again took a
            // second below the stop clip's threshold: the animator played stop, stance, start — the character squatted
            const float WARY = 1.6f;
            var glide = new BodyGlide();
            for (int i = 0; i < 60; i++)
                glide.Step(Vector3.right * WARY, false, Vector3.zero, ACCELERATION, DT, out _);

            float slowest = float.MaxValue;
            int steps = 0;
            while (Vector3.Angle(glide.Velocity, Vector3.left) > 1f && steps++ < 200)
            {
                glide.Step(Vector3.left * WARY, false, Vector3.zero, ACCELERATION, DT, out _);
                slowest = Mathf.Min(slowest, glide.Velocity.magnitude);
            }

            Assert.That(slowest, Is.EqualTo(WARY).Within(1e-3f), "It keeps its pace through the turn");
            Assert.LessOrEqual(steps * DT, 180f / BodyGlide.TURN_RATE + 2f * DT, "And faces the new way at its turn rate");
        }

        [Test]
        public void Glide_Halt_NoSpeedCarriesOver()
        {
            var glide = new BodyGlide();
            glide.Step(Ahead, false, Vector3.zero, ACCELERATION, DT, out _);

            glide.Halt();

            Assert.AreEqual(Vector3.zero, glide.Step(null, false, Vector3.zero, ACCELERATION, DT, out _));
        }

        [Test]
        public void NavMeshBody_TurnSpeed_IsTheCharactersOwnStatus_NotACopyOnTheBlackboard()
        {
            var body = new NavMeshBody { TurnSpeed = 300f };
            AICharacter character = Make(body);

            Assert.IsTrue(character.TryGetStat(CharacterStat.TurnSpeed, out float turn));
            Assert.That(turn, Is.EqualTo(300f));
            Assert.IsFalse(character.Blackboard.Has(NavMeshMover.TURN_SPEED), "Step 90: the numbers mirror is gone");
        }

        // A quarter turn clip at its own pace: slow start, fast middle, settling end — 90° in all
        private static float[] QuarterTurnClip()
        {
            const int FRAMES = 30;
            var raw = new float[FRAMES];
            float last = 0f;
            for (int i = 0; i < FRAMES; i++)
            {
                float t = (i + 1f) / FRAMES;
                float turned = 90f * t * t * (3f - 2f * t);
                raw[i] = -(turned - last);                 // a turn to the left
                last = turned;
            }

            return raw;
        }

        [TestCase(60f)]
        [TestCase(90f)]
        [TestCase(120f)]
        public void RootMotion_AnAimedTurn_KeepsTheClipsPace_EndsFacingTheTarget(float targetAt)
        {
            float[] clip = QuarterTurnClip();
            float facing = 0f;                              // degrees round the vertical; the target to the left
            float done = 0f;
            float mostInOneFrame = 0f;
            foreach (float raw in clip)
            {
                float need = -targetAt - facing;
                float yaw = NavMeshMover.AimedTurn(raw, need, TurnToTargetTask.TURN - done);
                done += Mathf.Abs(raw);
                facing += yaw;
                mostInOneFrame = Mathf.Max(mostInOneFrame, Mathf.Abs(yaw));
                Assert.LessOrEqual(Mathf.Abs(facing), targetAt + 0.01f, "Never round past the target");
            }

            Assert.AreEqual(-targetAt, facing, 0.5f, "The turn ends facing the target, however far round it was");
            float clipMost = clip.Max(Mathf.Abs);
            Assert.AreEqual(clipMost * targetAt / 90f, mostInOneFrame, clipMost * 0.2f,
                "The clip's pace, stretched to the way: fastest where the clip is");
        }

        [Test]
        public void RootMotion_ABlowsStepKeptOffItsTarget_StopsShortOfIt_ASideStepIsItsOwn()
        {
            const float CLOSEST = 0.9f;
            Vector3 toTarget = Vector3.forward * 1.5f;

            Vector3 step = NavMeshMover.KeptOff(Vector3.forward * 1f, toTarget, CLOSEST);
            Assert.AreEqual(1.5f - CLOSEST, step.z, 1e-4f, "Only as far as Closest from the target");

            step = NavMeshMover.KeptOff(new Vector3(0.3f, 0f, 0.2f), toTarget, CLOSEST);
            Assert.AreEqual(new Vector3(0.3f, 0f, 0.2f), step, "Short of the limit: the clip's own step");

            step = NavMeshMover.KeptOff(new Vector3(0.4f, 0f, 0.5f), Vector3.forward * 0.8f, CLOSEST);
            Assert.AreEqual(0.4f, step.x, 1e-4f, "Sideways it goes on");
            Assert.AreEqual(0f, step.z, 1e-4f, "Already too close: nothing more toward the target, never pushed back");

            step = NavMeshMover.KeptOff(Vector3.back * 0.5f, toTarget, CLOSEST);
            Assert.AreEqual(-0.5f, step.z, 1e-4f, "Away from the target: the clip's own");
        }

        [Test]
        public void RootMotion_TheClipsTurnIsDone_TheBodyTurnsNoFurther()
        {
            Assert.AreEqual(0f, NavMeshMover.AimedTurn(-3f, -40f, 0f), "Past the clip's own turn: nothing more");
        }

        [Test]
        public void RootMotion_Off_TheBodyDoesNotTakeTheClips_On_ItDoes()
        {
            Assert.IsFalse(new NavMeshBody().UsesRootMotion);
            var body = new NavMeshBody { ActionRootMotion = true };
            AICharacter character = Make(body);
            Assert.IsTrue(body.UsesRootMotion);
            Assert.IsTrue(((IClipAimActuator)body.Actuator).ActionsByClip, "A turn in place is the clip's");
            Assert.AreEqual(Vector3.zero, body.MoveByAnimation(Vector3.forward, Quaternion.identity, true, 0.02f),
                "Off the NavMesh the clip moves nothing");
            Assert.IsNotNull(character);
        }

        // ---------- full root motion (NavMeshBody.RootMotion): the clips carry the walk too ----------

        [Test]
        public void NavMeshBody_RootMotion_TheAgentNoLongerMovesTheBody_AndSaysSo()
        {
            var carried = new NavMeshBody { RootMotion = true };
            Make(carried);
            Assert.IsFalse(_go.GetComponent<NavMeshAgent>().updatePosition, "the animation puts the body, not the agent");
            Assert.IsTrue(carried.UsesRootMotion);
            Assert.IsTrue(carried.WalksByAnimation, "the walk is the clips' too");
            Assert.IsTrue(((IClipAimActuator)carried.Actuator).ActionsByClip, "and so are the actions");
            Object.DestroyImmediate(_go);

            var walked = new NavMeshBody();
            Make(walked);
            Assert.IsTrue(_go.GetComponent<NavMeshAgent>().updatePosition, "without root motion the agent moves it as before");
            Assert.IsFalse(walked.UsesRootMotion);
            Assert.IsFalse(walked.WalksByAnimation);
        }

        [Test]
        public void RootMotionSteer_TheWalkGoesThePathsWay_AsFarAsTheClip_StretchedNoMoreThanItsGive()
        {
            const float GIVE = 0.25f;
            Vector3 clip = Vector3.forward * 0.05f;                         // the clip went 5 cm forward this frame

            Vector3 slower = NavMeshMover.Steer(clip, Vector3.right * 0.01f, GIVE);
            Vector3 faster = NavMeshMover.Steer(clip, Vector3.right * 0.5f, GIVE);
            Vector3 even = NavMeshMover.Steer(clip, Vector3.right * 0.045f, GIVE);

            Assert.That(Vector3.Angle(slower, Vector3.right), Is.LessThan(0.01f), "the way is the path's: the crowd's way round is kept");
            Assert.That(slower.magnitude, Is.EqualTo(0.05f * (1f - GIVE)).Within(1e-5f), "asked slower: shortened by its give, no more");
            Assert.That(faster.magnitude, Is.EqualTo(0.05f * (1f + GIVE)).Within(1e-5f), "asked faster: stretched by its give, no more");
            Assert.That(even.magnitude, Is.EqualTo(0.045f).Within(1e-5f), "within the give: the path's pace exactly");
        }

        [Test]
        public void RootMotionSteer_NothingAsked_TheClipsOwnStep_TheClipStanding_NoStep()
        {
            Vector3 stopClip = new Vector3(0.02f, 0f, 0.01f);

            Assert.That(Vector3.Angle(NavMeshMover.Steer(stopClip, Vector3.zero, 0.25f), stopClip), Is.LessThan(0.01f),
                "a stop clip puts its feet down its own way");
            Assert.That(NavMeshMover.Steer(Vector3.zero, Vector3.forward, 0.25f), Is.EqualTo(Vector3.zero),
                "the stance does not slide off on its own: the start clip begins the walk");
        }
    }
}
