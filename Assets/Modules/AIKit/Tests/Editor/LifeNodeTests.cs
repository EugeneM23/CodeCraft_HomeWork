using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// The nodes of peaceful life and of the call for help, on real agents with a fake body: Perform (a step of the
    /// day — walk to a place, show an action for a while), Chat (a talk at a meeting place, the seats kept by the
    /// world's roster), Call For Help (a shout over the signal bus that the others' Calls For Help sensors, wired into
    /// their Detection, take as their own sight), the Time Of Day sensor (a clock seen while the hour is in its period)
    /// with the decisions that use it — Sleep and Daily Routine —, Patrol Area, Patrol and Wait. Time comes only from
    /// the steps, every random from the agent's seed; what is expected is counted from the nodes' settings.
    /// </summary>
    public sealed class LifeNodeTests
    {
        private const float DT = 0.02f;
        private const string PLACE = "Place";
        private const string TARGET = "Target";
        private const string SEEN = "Seen";
        private const string CLOCK = "Clock";
        private const string AREA = "Area";
        private const string BEDTIME = "Bedtime";
        private const string WAKE_UP = "Wake Up";

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

        // ---------- Perform ----------

        [Test]
        public void Perform_NoPlace_ActsWhereItStandsForItsDuration_ThenDone()
        {
            var rig = new GraphRig(trace: false);
            var standing = new Vector3(2f, 0f, 3f);
            rig.Actuator.Position = standing;
            var actions = new CharacterActionsWatch(rig.Context);
            var perform = new PerformTask { Animation = "Dig", Duration = 1.2f };
            float duration = perform.Duration.Get(null);

            int steps = StepsToEnd(rig, perform, duration + 5f, out TaskStatus status);

            Assert.AreEqual(TaskStatus.Success, status, "Done");
            CollectionAssert.AreEqual(new[] { "Dig" }, actions.Played, "The body showed the action by its name");
            Assert.AreEqual(1, actions.Endings, "At the end the work is put down...");
            Assert.AreEqual(0, actions.Drops, "...not dropped");
            Assert.That(steps * DT, Is.InRange(duration, duration + 4 * DT), "For its Duration");
            Assert.AreEqual(standing, rig.Actuator.Position, "Where it stood: no place, no walk");
        }

        [Test]
        public void Perform_WithAPlace_WalksThereAtItsPace_TurnsTheWayThePlaceLooks_ThenActs()
        {
            var rig = new GraphRig(trace: false);
            var walker = new Walker(rig.Actuator);
            rig.Context.Navigation = walker;
            var actions = new CharacterActionsWatch(rig.Context);
            Transform place = NewObject("Anvil", new Vector3(6f, 0f, 0f));
            place.rotation = Quaternion.LookRotation(Vector3.back);         // it looks across the way there
            rig.Context.Blackboard.Set(PLACE, place);
            var perform = new PerformTask { Place = PLACE, Animation = "Hammer", Duration = 0.5f };

            Assert.Greater(StepsUntil(rig, perform, () => actions.Played.Count > 0, 10f), 0, "It got to work");
            Vector3 workedAt = rig.Actuator.Position;
            Assert.LessOrEqual(Flat(place.position - workedAt).magnitude, perform.ArriveDistance.Get(null) + 1e-4f,
                "...at the place");
            CollectionAssert.IsNotEmpty(walker.Paces);
            Assert.That(walker.Paces, Is.All.EqualTo(perform.Pace.Get(null)), "Walked there at its Pace, not at a run");

            StepsToEnd(rig, perform, 5f, out TaskStatus status);

            Assert.AreEqual(TaskStatus.Success, status, "Done");
            Assert.Less(Vector3.Angle(rig.Actuator.Facing, place.forward), 1f, "Turned the way the place looks");
            Assert.AreEqual(workedAt, rig.Actuator.Position, "Stood still at its work");
            Assert.AreEqual(1f, walker.Pace, "Full speed again for whatever comes next");
        }

        [Test]
        public void Perform_SomebodyStandsOnThePlace_ActsFromBesideIt_AfterTheStuckTime()
        {
            var rig = new GraphRig(trace: false);
            rig.Context.Navigation = new Walker(rig.Actuator) { Blocked = true };     // not a step closer: it is taken
            var actions = new CharacterActionsWatch(rig.Context);
            var perform = new PerformTask { Place = PLACE, Animation = "Gather", Duration = 0.4f };
            float arrive = perform.ArriveDistance.Get(null);
            Transform place = NewObject("Bush", new Vector3(2f * arrive, 0f, 0f));      // right beside it, not on it
            rig.Context.Blackboard.Set(PLACE, place);
            float stuck = Constant<PerformTask>("STUCK_TIME");

            int started = StepsUntil(rig, perform, () => actions.Played.Count > 0, stuck + 1f);

            Assert.That(started * DT, Is.InRange(stuck - DT, stuck + 2 * DT),
                "After the stuck time next to the place it works from where it stands");
            Assert.Greater(Flat(place.position - rig.Actuator.Position).magnitude, arrive, "...not on the place itself");
            StepsToEnd(rig, perform, 5f, out TaskStatus status);
            Assert.AreEqual(TaskStatus.Success, status, "Done all the same");
        }

        [Test]
        public void Perform_NowhereToGo_Failed()
        {
            var rig = new GraphRig(trace: false);
            var actions = new CharacterActionsWatch(rig.Context);

            Assert.AreEqual(TaskStatus.Failure, new PerformTask { Place = PLACE }.Tick(rig.Context, DT),
                "The place's variable is not on the blackboard");
            rig.Context.Blackboard.Set<Transform>(PLACE, null);
            Assert.AreEqual(TaskStatus.Failure, new PerformTask { Place = PLACE }.Tick(rig.Context, DT),
                "The variable holds no object");

            rig.Context.Blackboard.Set(PLACE, NewObject("Field", new Vector3(5f, 0f, 0f)));
            rig.Context.Navigation = new Walker(rig.Actuator) { CanReach = _ => false };
            Assert.AreEqual(TaskStatus.Failure, new PerformTask { Place = PLACE }.Tick(rig.Context, DT), "No way to it");
            rig.Context.Navigation = null;
            Assert.AreEqual(TaskStatus.Failure, new PerformTask { Place = PLACE }.Tick(rig.Context, DT),
                "Nothing to walk with");
            CollectionAssert.IsEmpty(actions.Played, "Nothing was done anywhere");
            Assert.AreEqual(Vector3.zero, rig.Actuator.Position, "Nor did it go anywhere");

            rig.Context.Navigation = new Walker(rig.Actuator) { Blocked = true };     // a way that never gets there
            float walkLimit = Constant<PerformTask>("WALK_LIMIT");
            int steps = StepsToEnd(rig, new PerformTask { Place = PLACE }, walkLimit + 1f, out TaskStatus status);
            Assert.AreEqual(TaskStatus.Failure, status, "Never got near: the place is out of reach");
            Assert.That(steps * DT, Is.InRange(walkLimit - 2 * DT, walkLimit + 3 * DT), "...after the walk limit");
        }

        [Test]
        public void Perform_GivesUpOnTheWay_Failed_AndCallsTheWalkOff()
        {
            var rig = new GraphRig(trace: false);
            var walker = new Walker(rig.Actuator);
            rig.Context.Navigation = walker;
            rig.Context.Blackboard.Set(PLACE, NewObject("Field", new Vector3(6f, 0f, 0f)));
            var perform = new PerformTask { Place = PLACE };
            Assume.That(KeepsRunning(rig, perform, 0.5f), "On its way");

            walker.GiveUp();                                    // the path turned out to lead nowhere

            Assert.AreEqual(TaskStatus.Failure, Step(rig, perform), "Out of reach: Failed");
            Assert.AreEqual(NavStatus.Idle, walker.Status,
                "The path is called off on every way out, a failure too (handbook §2.1): a NavMesh agent walks on " +
                "by itself after the node is over");
        }

        [Test]
        public void Perform_DurationZero_ActsUntilTheAnimationEndsByItself()
        {
            var rig = new GraphRig(trace: false);
            var actions = new CharacterActionsWatch(rig.Context);
            var perform = new PerformTask { Animation = "Fish Cast", Duration = 0f };
            const float cast = 1f;                              // how long the body's animation plays: not the node's

            Assert.IsTrue(KeepsRunning(rig, perform, cast), "Acts while the animation plays");
            actions.IsPlaying = false;                          // the throw is over by itself
            int steps = StepsToEnd(rig, perform, 1f, out TaskStatus status);

            Assert.AreEqual(TaskStatus.Success, status, "Done");
            Assert.LessOrEqual(steps, 3, "...as soon as the animation has ended");
            CollectionAssert.AreEqual(new[] { "Fish Cast" }, actions.Played);
        }

        [Test]
        public void Perform_TheEndingOfTheWork_IsWaitedFor_AtMostTheEndLimit()
        {
            var rig = new GraphRig(trace: false);
            var actions = new CharacterActionsWatch(rig.Context) { HasEnding = true };
            var perform = new PerformTask { Animation = "Plow", Duration = 0.4f };
            float duration = perform.Duration.Get(null);

            int putDown = StepsUntil(rig, perform, () => actions.Endings > 0, 2f);
            Assert.That(putDown * DT, Is.InRange(duration, duration + 3 * DT), "The work is put down after its Duration");
            Assert.IsTrue(KeepsRunning(rig, perform, 0.5f), "...its ending plays: not done yet");
            actions.IsPlaying = false;
            Assert.AreEqual(TaskStatus.Success, Step(rig, perform), "Done once the work is down");

            var stubborn = new CharacterActionsWatch(rig.Context) { HasEnding = true };    // an ending that never ends
            var again = new PerformTask { Animation = "Plow", Duration = 0.4f };
            Assume.That(StepsUntil(rig, again, () => stubborn.Endings > 0, 2f), Is.GreaterThan(0));
            float endLimit = Constant<PerformTask>("END_LIMIT");
            int waited = StepsToEnd(rig, again, endLimit + 1f, out TaskStatus status);
            Assert.AreEqual(TaskStatus.Success, status, "Not waited for forever");
            Assert.That(waited * DT, Is.InRange(endLimit - DT, endLimit + 2 * DT), "...the end limit at most");
        }

        [Test]
        public void Perform_TornAway_DropsTheWorkAtOnce()
        {
            var rig = new GraphRig(trace: false);
            var walker = new Walker(rig.Actuator);
            rig.Context.Navigation = walker;
            var actions = new CharacterActionsWatch(rig.Context) { HasEnding = true };
            rig.Context.Blackboard.Set(PLACE, NewObject("Field", new Vector3(1f, 0f, 0f)));
            var perform = new PerformTask { Place = PLACE, Animation = "Plow", Duration = 10f };
            Assume.That(StepsUntil(rig, perform, () => actions.Played.Count > 0, 5f), Is.GreaterThan(0), "At work");
            int stops = rig.Actuator.Stops;

            perform.Interrupt(rig.Context);                     // a reflex takes the agent away

            Assert.AreEqual(1, actions.Drops, "The work is dropped at once");
            Assert.AreEqual(0, actions.Endings, "...with no time to put it down");
            Assert.IsFalse(actions.IsPlaying);
            Assert.Greater(rig.Actuator.Stops, stops, "The body stops");
            Assert.AreEqual(1f, walker.Pace, "Full speed for whatever comes next");
            Assert.IsFalse(perform.IsRunningFor(rig.Context));
        }

        // ---------- Chat ----------

        [Test]
        public void Chat_TwoAgentsOfOneWorld_SitAroundThePlaceFacingIt_TalkForTheDuration_Done()
        {
            var world = new AIWorld();
            Transform place = NewObject("Well", Vector3.zero);
            GraphRig[] talkers = { RigIn(world, new Vector3(-5f, 0f, 0f)), RigIn(world, new Vector3(5f, 0f, 0f)) };
            CharacterActionsWatch[] actions = talkers.Select(talker => new CharacterActionsWatch(talker.Context)).ToArray();
            foreach (GraphRig talker in talkers)
                talker.Context.Blackboard.Set(PLACE, place);

            var chat = new ChatTask { Place = PLACE, Animation = "Talk", Duration = 2f, WaitTime = 3f };
            int[] started = { -1, -1 };
            int[] ended = { -1, -1 };
            Vector3[] seated = null;
            Vector3[] facing = null;

            TaskStatus[] status = Talk(chat, talkers, 15f, (step, _) =>
            {
                for (int i = 0; i < talkers.Length; i++)
                {
                    if (started[i] < 0 && actions[i].Played.Count > 0)
                        started[i] = step;
                    if (ended[i] < 0 && actions[i].Endings > 0)
                        ended[i] = step;
                }

                if (seated == null && started[0] >= 0 && started[1] >= 0)
                {
                    seated = talkers.Select(rig => rig.Actuator.Position).ToArray();
                    facing = talkers.Select(rig => rig.Actuator.Facing).ToArray();
                }
            });

            CollectionAssert.AreEqual(new[] { TaskStatus.Success, TaskStatus.Success }, status, "Both talked: Done");
            Assert.IsNotNull(seated, "They talked at the same time");
            float duration = chat.Duration.Get(null);
            float radius = chat.Radius.Get(null);
            for (int i = 0; i < talkers.Length; i++)
            {
                CollectionAssert.AreEqual(new[] { "Talk" }, actions[i].Played, "The body showed the talk by its name");
                Assert.That((ended[i] - started[i] + 1) * DT, Is.InRange(duration - DT, duration + 2 * DT),
                    "They talked for the Duration");
                Vector3 toMiddle = Flat(place.position - seated[i]);
                Assert.Less(toMiddle.magnitude, radius * 2f, "Around the middle of the place");
                Assert.Greater(Vector3.Dot(facing[i], toMiddle.normalized), 0.99f, "Facing the middle");
            }

            Assert.Less(Vector3.Dot(seated[0] - place.position, seated[1] - place.position), 0f,
                "Two seats across the place from each other");
            Assert.AreEqual(0, world.Roster.EngagementOf(place).Tokens.Used(ChatTask.POOL), "Both seats are free again");
        }

        [Test]
        public void Chat_AloneInItsWorld_WaitsItsWaitTimeOnASeat_ThenAlone()
        {
            var world = new AIWorld();
            Transform place = NewObject("Well", Vector3.zero);
            GraphRig rig = RigIn(world, new Vector3(-5f, 0f, 0f));
            var actions = new CharacterActionsWatch(rig.Context);
            rig.Context.Blackboard.Set(PLACE, place);
            var chat = new ChatTask { Place = PLACE, Animation = "Talk", WaitTime = 2f };
            float waitTime = chat.WaitTime.Get(null);
            int standing = 0;

            TaskStatus status = TaskStatus.Running;
            for (int i = 0; i < Steps(waitTime + 10f) && status == TaskStatus.Running; i++)
            {
                world.BeginStep(DT);
                Vector3 before = rig.Actuator.Position;
                status = Step(rig, chat);
                if ((rig.Actuator.Position - before).sqrMagnitude < 1e-12f)
                    standing++;
            }

            Assert.AreEqual(TaskStatus.Failure, status, "Nobody came: Alone");
            CollectionAssert.IsEmpty(actions.Played, "No talk without somebody to talk to");
            Assert.That(standing * DT, Is.InRange(waitTime - DT, waitTime + 2 * DT), "It waited its WaitTime");
            Assert.Less(Flat(place.position - rig.Actuator.Position).magnitude, chat.Radius.Get(null) * 2f,
                "...on a seat at the place");
            Assert.IsFalse(world.Roster.EngagementOf(place).Tokens.Holds(rig.Agent, ChatTask.POOL),
                "The seat is free again");
        }

        [Test]
        public void Chat_EverySeatTaken_TheNextOneWaitsAndLeavesAlone_WhileTheOthersTalk()
        {
            var world = new AIWorld();
            Transform place = NewObject("Bench", Vector3.zero);
            GraphRig[] rigs =
            {
                RigIn(world, new Vector3(-5f, 0f, 0f)),
                RigIn(world, new Vector3(5f, 0f, 0f)),
                RigIn(world, new Vector3(0f, 0f, -8f))         // the last to come
            };
            CharacterActionsWatch[] actions = rigs.Select(one => new CharacterActionsWatch(one.Context)).ToArray();
            foreach (GraphRig one in rigs)
                one.Context.Blackboard.Set(PLACE, place);

            var chat = new ChatTask { Place = PLACE, Animation = "Talk", Seats = 2, Duration = 6f, WaitTime = 2f };
            GraphRig late = rigs[2];
            bool lateHeldASeat = false;
            bool othersTalkingWhenItLeft = false;
            int leftAt = -1;

            TaskStatus[] status = Talk(chat, rigs, 15f, (step, now) =>
            {
                lateHeldASeat |= world.Roster.EngagementOf(place).Tokens.Holds(late.Agent, ChatTask.POOL);
                if (leftAt >= 0 || now[2] == TaskStatus.Running)
                    return;

                leftAt = step;
                othersTalkingWhenItLeft = now[0] == TaskStatus.Running && now[1] == TaskStatus.Running &&
                                          actions[0].Played.Count > 0 && actions[1].Played.Count > 0;
            });

            Assert.AreEqual(TaskStatus.Failure, status[2], "Both seats were taken: the last one left Alone");
            Assert.IsFalse(lateHeldASeat, "It never got a seat");
            CollectionAssert.IsEmpty(actions[2].Played, "...nor a word");
            Assert.IsTrue(othersTalkingWhenItLeft, "It left while the two talked");
            Assert.AreEqual(TaskStatus.Success, status[0]);
            Assert.AreEqual(TaskStatus.Success, status[1]);
        }

        [Test]
        public void Chat_NowhereToMeet_Alone()
        {
            var rig = new GraphRig(trace: false);

            Assert.AreEqual(TaskStatus.Failure, new ChatTask().Tick(rig.Context, DT), "No place named");
            Assert.AreEqual(TaskStatus.Failure, new ChatTask { Place = PLACE }.Tick(rig.Context, DT),
                "The variable holds no object");

            rig.Context.Blackboard.Set(PLACE, NewObject("Far Well", new Vector3(20f, 0f, 0f)));
            rig.Context.Navigation = new Walker(rig.Actuator) { CanReach = _ => false };
            Assert.AreEqual(TaskStatus.Failure, new ChatTask { Place = PLACE }.Tick(rig.Context, DT), "No way there");
            rig.Context.Navigation = null;
            Assert.AreEqual(TaskStatus.Failure, new ChatTask { Place = PLACE }.Tick(rig.Context, DT),
                "Nothing to walk with");
            Assert.AreEqual(Vector3.zero, rig.Actuator.Position);
        }

        [Test]
        public void Chat_GivesUpOnTheWay_Alone_AndCallsTheWalkOff()
        {
            var rig = new GraphRig(trace: false);
            var walker = new Walker(rig.Actuator);
            rig.Context.Navigation = walker;
            rig.Context.Blackboard.Set(PLACE, NewObject("Far Well", new Vector3(10f, 0f, 0f)));
            var chat = new ChatTask { Place = PLACE };
            Assume.That(KeepsRunning(rig, chat, 0.5f), "On its way");

            walker.GiveUp();                                    // the path turned out to lead nowhere

            Assert.AreEqual(TaskStatus.Failure, Step(rig, chat), "No way there: Alone");
            Assert.AreEqual(NavStatus.Idle, walker.Status,
                "The path is called off on every way out, a failure too (handbook §2.1): a NavMesh agent walks on " +
                "by itself after the node is over");
        }

        [Test]
        public void Chat_NoWorldGiven_TheAgentsJoinTheScenesWorld_AndTalk()
        {
            using (new TempScene())
            {
                Transform place = NewObject("Well", Vector3.zero);
                GraphRig[] talkers =
                {
                    Woken(new GraphRig(trace: false), new Vector3(-5f, 0f, 0f)),
                    Woken(new GraphRig(trace: false), new Vector3(5f, 0f, 0f))
                };
                foreach (GraphRig rig in talkers)
                    rig.Context.Blackboard.Set(PLACE, place);
                Assume.That(talkers[0].Agent.World, Is.Null);
                var chat = new ChatTask { Place = PLACE, Duration = 1f, WaitTime = 3f };

                TaskStatus[] status = Talk(chat, talkers, 10f);

                Assert.IsNotNull(talkers[0].Agent.World, "Asking for a seat, it joined the scene's world");
                Assert.AreSame(talkers[0].Agent.World, talkers[1].Agent.World, "One world for the scene: they met");
                CollectionAssert.AreEqual(new[] { TaskStatus.Success, TaskStatus.Success }, status, "...and talked");
            }
        }

        // ---------- Call For Help ----------

        [Test]
        public void CallForHelp_HeardByTheAgentsWithinItsRadius_NotBeyond_NotByTheCaller()
        {
            var bus = new SignalBus();
            var call = new CallForHelpTask { Target = TARGET };
            float radius = call.Radius.Get(null);
            GraphRig caller = Listening(bus, Vector3.zero);
            GraphRig near = Listening(bus, new Vector3(radius - 1f, 0f, 0f));
            GraphRig far = Listening(bus, new Vector3(0f, 0f, radius + 1f));
            Transform enemy = Enemy(new Vector3(2f, 0f, 0f));
            caller.Context.Blackboard.Set(TARGET, enemy);

            Assert.AreEqual(TaskStatus.Success, call.Tick(caller.Context, DT), "Done at once");

            Assert.AreEqual(1, call.HeardBy(caller.Context));
            Assert.IsTrue(near.Context.HasSignal(CallForHelpTask.SIGNAL), "Within its radius: heard");
            Assert.AreSame(enemy, near.Context.SignalSubject(CallForHelpTask.SIGNAL), "...with what it is about");
            Assert.IsFalse(far.Context.HasSignal(CallForHelpTask.SIGNAL), "Beyond its radius: not heard");
            Assert.IsFalse(caller.Context.HasSignal(CallForHelpTask.SIGNAL), "The caller does not hear itself");
        }

        [Test]
        public void CallForHelp_NoTargetOrNoBus_StillDone_NobodyHears()
        {
            var bus = new SignalBus();
            GraphRig caller = Listening(bus, Vector3.zero);
            GraphRig ally = Listening(bus, new Vector3(2f, 0f, 0f));
            var call = new CallForHelpTask { Target = TARGET };

            Assert.AreEqual(TaskStatus.Success, call.Tick(caller.Context, DT), "Nothing to call about: done anyway");
            Assert.AreEqual(0, call.HeardBy(caller.Context));
            Assert.IsFalse(ally.Context.HasSignal(CallForHelpTask.SIGNAL), "No call went out");

            var alone = new AgentContext(null, null, 1);        // no agent, no bus: nobody to shout to
            alone.Blackboard.Set(TARGET, Enemy(Vector3.one));
            Assert.AreEqual(TaskStatus.Success, call.Tick(alone, DT));
            Assert.AreEqual(0, call.HeardBy(alone));
        }

        [Test]
        public void CallForHelp_TheCalledAgentsBrainTakesWhatItWasCalledAbout_ForRemember_ThenLetsGo()
        {
            var bus = new SignalBus();
            GraphRig caller = Listening(bus, Vector3.zero);
            GraphRig ally = Listening(bus, new Vector3(6f, 0f, 0f));
            ally.ThinkInterval = 0f;
            Transform enemy = Enemy(new Vector3(-3f, 0f, 0f));
            ally.Context.Objects = new Finder(enemy);           // in the scene, but the ally has no eyes: only the call
            var heard = new CallsForHelpSensorTask { Remember = 2f };
            float remember = heard.Remember.Get(null);
            var detection = new DetectionTask(heard) { StoreIn = SEEN, Interval = 0f };
            ally.SetGraph(HelpGraph(detection));
            Assert.IsTrue(ally.StepUntil(() => ally.StateName == "Idle", 1f), "Set-up: nothing heard, its day goes on");
            ally.StepSeconds(BehaviorGraphInstance.MIN_DECISION_TIME);
            caller.Context.Blackboard.Set(TARGET, enemy);

            new CallForHelpTask { Target = TARGET }.Tick(caller.Context, DT);
            float heardAt = ally.Context.LocalTime;

            Assert.IsTrue(ally.StepUntil(() => ally.StateName == "Help", 5 * DT),
                "The call is the ally's sight: its Brain takes the decision about the target");
            Assert.AreSame(enemy, detection.SeenBy(ally.Context), "Detection holds what the caller called about");
            Assert.IsTrue(ally.StepUntil(() => ally.StateName == "Idle", remember + 1f), "Never seen itself: let go");
            Assert.That(ally.Context.LocalTime - heardAt, Is.InRange(remember, remember + 4 * DT), "...after Remember");
            Assert.IsNull(detection.SeenBy(ally.Context));
        }

        [Test]
        public void CallForHelp_TheCalledAgentSeesTheTargetItself_KeepsItPastRemember()
        {
            var bus = new SignalBus();
            GraphRig caller = Listening(bus, Vector3.zero);
            GraphRig ally = Listening(bus, new Vector3(3f, 0f, 0f));
            var eyes = new RadiusSensorTask { Radius = 5f, LoseRadius = 7f };
            var heard = new CallsForHelpSensorTask { Remember = 1f };
            float remember = heard.Remember.Get(null);
            var detection = new DetectionTask(eyes, heard) { StoreIn = SEEN, Interval = 0f };
            Transform enemy = Enemy(new Vector3(20f, 0f, 0f));      // out of the ally's sight, within the call's reach
            Assume.That(Flat(enemy.position - ally.Actuator.Position).magnitude, Is.LessThan(heard.Within.Get(null)));
            ally.Context.Objects = new Finder(enemy);
            caller.Context.Blackboard.Set(TARGET, enemy);
            new CallForHelpTask { Target = TARGET }.Tick(caller.Context, DT);

            Run(ally, detection, remember * 0.5f);
            Assert.AreSame(enemy, detection.SeenBy(ally.Context), "Held on the call");

            enemy.position = ally.Actuator.Position + Vector3.right * (eyes.Radius.Get(null) - 1f);
            Run(ally, detection, remember * 2f);
            Assert.AreSame(enemy, detection.SeenBy(ally.Context), "Seen itself in time: kept by the usual rule");

            enemy.position = ally.Actuator.Position + Vector3.right * (eyes.LoseRadius.Get(null) + 1f);
            Run(ally, detection, DT);
            Assert.IsNull(detection.SeenBy(ally.Context), "Out of sight, and the call long forgotten: lost");
        }

        [Test]
        public void CallForHelp_AnAgentThatHeardACall_DoesNotPassItOn_ForItsQuietTime()
        {
            var bus = new SignalBus();
            GraphRig caller = Listening(bus, Vector3.zero);
            GraphRig ally = Listening(bus, new Vector3(5f, 0f, 0f));
            Transform enemy = Enemy(new Vector3(-2f, 0f, 0f));
            caller.Context.Blackboard.Set(TARGET, enemy);
            ally.Context.Blackboard.Set(TARGET, enemy);
            var call = new CallForHelpTask { Target = TARGET };     // one node, both agents' graphs
            float quiet = Constant<CallForHelpTask>("RELAY_SILENCE");
            call.Tick(caller.Context, DT);
            Assume.That(ally.Context.HasSignal(CallForHelpTask.SIGNAL), "The ally heard the call");

            Assert.AreEqual(TaskStatus.Success, call.Tick(ally.Context, DT), "Done all the same");
            Assert.AreEqual(0, call.HeardBy(ally.Context), "Came because it was called: does not pass the call on");
            ally.Context.Advance(quiet - 2 * DT);
            call.Tick(ally.Context, DT);
            Assert.AreEqual(0, call.HeardBy(ally.Context), "Still in its quiet time");
            Assert.IsFalse(caller.Context.HasSignal(CallForHelpTask.SIGNAL), "...so nothing came back to the caller");

            ally.Context.Advance(3 * DT);
            call.Tick(ally.Context, DT);
            Assert.AreEqual(1, call.HeardBy(ally.Context), "Its quiet time is over: it calls like anybody");
            Assert.IsTrue(caller.Context.HasSignal(CallForHelpTask.SIGNAL));
        }

        // ---------- Calls For Help, the sensor ----------

        [Test]
        public void CallsForHelpSensor_SeesTheCalledAboutObject_ForRememberSeconds_ThenNothing()
        {
            var bus = new SignalBus();
            GraphRig caller = Listening(bus, Vector3.zero);
            GraphRig ally = Listening(bus, new Vector3(4f, 0f, 0f));
            Transform enemy = Enemy(new Vector3(-3f, 0f, 0f));
            ally.Context.Objects = new Finder(enemy);
            var heard = new CallsForHelpSensorTask { Remember = 1.5f };
            float remember = heard.Remember.Get(null);
            var detection = new DetectionTask(heard) { StoreIn = SEEN, Interval = 0f };

            Step(ally, detection);
            Assert.IsNull(detection.SeenBy(ally.Context), "No call: nothing seen");

            caller.Context.Blackboard.Set(TARGET, enemy);
            new CallForHelpTask { Target = TARGET }.Tick(caller.Context, DT);
            float heardAt = ally.Context.LocalTime;
            Step(ally, detection);
            Assert.AreSame(enemy, detection.SeenBy(ally.Context), "Called about: seen");

            int steps = StepsUntil(ally, detection, () => detection.SeenBy(ally.Context) == null, remember + 1f);

            Assert.Greater(steps, 0, "Not seen for ever");
            Assert.That(ally.Context.LocalTime - heardAt, Is.InRange(remember - DT, remember + 2 * DT),
                "For Remember seconds after the call");
        }

        [Test]
        public void CallsForHelpSensor_OnlyOfDetectionsKind_OnlyWithinWithin_NotOnceGone_NotWithoutTheSensor()
        {
            var bus = new SignalBus();
            GraphRig caller = Listening(bus, Vector3.zero);
            GraphRig ally = Listening(bus, new Vector3(4f, 0f, 0f));
            var heard = new CallsForHelpSensorTask { Within = 12f };
            float within = heard.Within.Get(null);
            Transform enemy = Enemy(ally.Actuator.Position + Vector3.left * (within - 1f));
            ally.Context.Objects = new Finder(enemy);
            var kin = new DetectionTask(heard) { StoreIn = "Kin", Interval = 0f };
            var stranger = new DetectionTask(new CallsForHelpSensorTask())
            {
                StoreIn = "Stranger", Tag = "MainCamera", Interval = 0f
            };
            var deaf = new DetectionTask(new RadiusSensorTask()) { StoreIn = "Deaf", Interval = 0f };   // no Calls For Help
            Assume.That(enemy.tag, Is.EqualTo(kin.Tag));
            caller.Context.Blackboard.Set(TARGET, enemy);
            new CallForHelpTask { Target = TARGET }.Tick(caller.Context, DT);
            Assume.That(ally.Context.HasSignal(CallForHelpTask.SIGNAL), "The ally heard the call");

            foreach (DetectionTask detection in new[] { kin, stranger, deaf })
                Step(ally, detection);

            Assert.AreSame(enemy, kin.SeenBy(ally.Context), "Of Detection's kind, within reach: taken as if seen");
            Assert.IsNull(stranger.SeenBy(ally.Context), "Not of Detection's kind (its tag)");
            Assert.IsNull(deaf.SeenBy(ally.Context), "No Calls For Help wired into Detection: calls are not heard");

            enemy.position = ally.Actuator.Position + Vector3.left * (within + 1f);
            Step(ally, kin);
            Assert.IsNull(kin.SeenBy(ally.Context), "Farther than Within: not its business");

            enemy.position = ally.Actuator.Position + Vector3.left * (within - 1f);
            Step(ally, kin);
            Assert.AreSame(enemy, kin.SeenBy(ally.Context), "Within again, the call still fresh: seen again");

            enemy.gameObject.SetActive(false);
            Step(ally, kin);
            Assert.IsNull(kin.SeenBy(ally.Context), "Gone: nothing to come for");
        }

        // ---------- Time Of Day ----------

        [TestCase(23f, 22f, 6f, true)]
        [TestCase(2f, 22f, 6f, true)]
        [TestCase(22f, 22f, 6f, true)]
        [TestCase(6f, 22f, 6f, false)]
        [TestCase(12f, 22f, 6f, false)]
        [TestCase(8f, 8f, 17f, true)]
        [TestCase(16.5f, 8f, 17f, true)]
        [TestCase(17f, 8f, 17f, false)]
        [TestCase(7.9f, 8f, 17f, false)]
        public void TimeOfDay_Within_FromIncludedToExcluded_OverMidnightWhenToIsLess(float hour, float from, float to,
            bool within)
        {
            Assert.AreEqual(within, TimeSensorTask.Within(hour, from, to));
        }

        [Test]
        public void TimeOfDay_WritesTheClockWhileTheHourIsInItsPeriod_NothingOutside_NoticedAtTheNextLook()
        {
            var rig = new GraphRig(trace: false);
            Clock clock = NewClock(12f);
            rig.Context.Objects = new Finder(clock.transform);
            var sensor = new TimeSensorTask { StoreIn = CLOCK, From = 22f, To = 6f };
            float interval = sensor.Interval.Get(null);

            Step(rig, sensor);
            Assert.IsNull(sensor.SeenBy(rig.Context), "Noon: not in the night");

            clock.Hour = 23f;
            int noticed = StepsUntil(rig, sensor, () => sensor.SeenBy(rig.Context) == clock.transform, 2f);
            Assert.That(noticed * DT, Is.InRange(interval - DT, interval + 1.5f * DT),
                "The clock is written into the variable at the next look, one Interval later");

            clock.Hour = 3f;
            Run(rig, sensor, interval + 2 * DT);
            Assert.AreSame(clock.transform, sensor.SeenBy(rig.Context), "Past midnight: still the night");

            clock.Hour = 7f;
            Run(rig, sensor, interval + 2 * DT);
            Assert.IsNull(sensor.SeenBy(rig.Context), "Morning: the period is over, the variable is empty");
        }

        [Test]
        public void Sleep_TakenWhenTheNightFalls_OnceForTheWholeNight_TheDailyRoutineAgainInTheMorning()
        {
            var rig = new GraphRig(trace: false) { ThinkInterval = 0f };
            Clock clock = NewClock(12f);
            rig.Context.Objects = new Finder(clock.transform);
            var sensor = new TimeSensorTask { StoreIn = CLOCK, From = 22f, To = 6f };
            float interval = sensor.Interval.Get(null);
            rig.SetGraph(DayGraph(sensor));
            var entered = new List<string>();
            rig.Instance.StateChanged += (_, to) =>
            {
                if (to != null && !to.IsBrain && to.Reasoner == null)
                    entered.Add(to.DisplayName);            // the blocks: what the character does
            };

            rig.StepSeconds(1f);
            CollectionAssert.AreEqual(new[] { "Day" }, entered, "Noon: the daily routine");

            clock.Hour = 22.5f;
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Night", interval + 4 * DT), "Night fell: Sleep");
            Assert.IsInstanceOf<SleepReasoner>(rig.Instance.Decision?.Reasoner);
            rig.StepSeconds(3f);
            CollectionAssert.AreEqual(new[] { "Day", "Night" }, entered,
                "Once, at the moment it began — not again for as long as the night lasts");

            clock.Hour = 6.5f;
            Assert.IsTrue(rig.StepUntil(() => rig.StateName == "Day", interval + 4 * DT), "Morning: the daily routine");
            Assert.IsInstanceOf<DailyRoutineReasoner>(rig.Instance.Decision?.Reasoner);
            CollectionAssert.AreEqual(new[] { "Day", "Night", "Day" }, entered);
        }

        [Test]
        public void Sleep_TakenWhileItsConditionNightIsThere_WithNoConditionItAlwaysHolds()
        {
            var rig = new GraphRig(trace: false);
            Clock clock = NewClock(23f);
            var sleep = new SleepReasoner { Condition = new CompareCondition(CLOCK, VariableType.Transform) };
            var query = new ReasonerQuery(rig.Context, TargetFacts.None, false);

            Assert.IsFalse(sleep.HoldsWithCondition(query, out string whyNot), "The variable holds nothing");
            StringAssert.Contains(CLOCK, whyNot, "It says the condition that does not hold");

            rig.Context.Blackboard.Set(CLOCK, clock.transform);
            Assert.IsTrue(sleep.HoldsWithCondition(query, out _), "The variable holds the clock: the night");

            Assert.IsTrue(Holds(new SleepReasoner(), rig.Context, out _),
                "No condition wired in: every decision holds by itself");
            Assert.IsFalse(sleep.NeedsTarget, "It needs no target of the Brain");
        }

        [Test]
        public void DailyRoutine_AlwaysHolds()
        {
            var routine = new DailyRoutineReasoner();
            var rig = new GraphRig(trace: false);

            Assert.IsTrue(Holds(routine, rig.Context, out string whyNot), "Nothing more important: the day's work");
            Assert.IsNull(whyNot);
            Assert.IsTrue(Holds(routine, null, out _), "No agent even");
            Assert.IsTrue(routine.Holds(new ReasonerQuery(rig.Context, TargetFacts.None, true), out _), "Taken: it keeps");
            Assert.IsFalse(routine.NeedsTarget, "It needs no target of the Brain");
        }

        [Test]
        public void TimeOfDay_EveryAgentKeepsItsOwnHours_OnItsBlackboard()
        {
            Clock clock = NewClock(22f);
            var early = new GraphRig(trace: false);
            var late = new GraphRig(trace: false);
            early.Context.Objects = late.Context.Objects = new Finder(clock.transform);
            early.Context.Blackboard.Set(BEDTIME, 21f);
            early.Context.Blackboard.Set(WAKE_UP, 6f);
            late.Context.Blackboard.Set(BEDTIME, 23f);
            late.Context.Blackboard.Set(WAKE_UP, 7.5f);
            var sensor = new TimeSensorTask                     // one node of one graph, read by both agents
            {
                StoreIn = CLOCK,
                Interval = 0f,
                From = BlackboardValue<float>.Variable(BEDTIME),
                To = BlackboardValue<float>.Variable(WAKE_UP)
            };

            bool Asleep(GraphRig rig)
            {
                Step(rig, sensor);
                return sensor.SeenBy(rig.Context) != null;
            }

            Assert.IsTrue(Asleep(early), "22:00 is past its bedtime");
            Assert.IsFalse(Asleep(late), "22:00 is before its bedtime");
            clock.Hour = 23.5f;
            Assert.IsTrue(Asleep(early));
            Assert.IsTrue(Asleep(late));
            clock.Hour = 6.5f;
            Assert.IsFalse(Asleep(early), "6:30 is past its waking hour");
            Assert.IsTrue(Asleep(late), "6:30 is before its waking hour");
        }

        [Test]
        public void TimeOfDay_NoClockOfItsKind_SeesNothing()
        {
            var rig = new GraphRig(trace: false);
            var sensor = new TimeSensorTask { StoreIn = CLOCK, From = 0f, To = 24f };     // the whole day long
            Transform plain = NewObject("Lamp Post", Vector3.zero, sensor.Tag);     // of its kind, but no clock on it
            Clock stray = NewClock(12f, "Untagged");                                // a clock, but not of its kind

            rig.Context.Objects = new Finder(plain, stray.transform);
            Run(rig, sensor, 2f);
            Assert.IsNull(sensor.SeenBy(rig.Context), "No clock of its kind: no hour to see");

            rig.Context.Objects = new Finder();
            Run(rig, sensor, 2f);
            Assert.IsNull(sensor.SeenBy(rig.Context), "Nothing at all in the scene");

            Clock clock = NewClock(12f);
            rig.Context.Objects = new Finder(clock.transform);
            Run(rig, sensor, 2f);
            Assert.AreSame(clock.transform, sensor.SeenBy(rig.Context), "A clock of its kind is seen the whole day");
        }

        [Test]
        public void TimeOfDay_DoesNotTakeACallForHelp()
        {
            var rig = new GraphRig(trace: false);
            rig.Agent.SetSignals(new SignalBus());              // it can hear: no scene's bus is made for it
            Clock clock = NewClock(12f);                        // noon: nothing for a night sensor to see
            rig.Context.Objects = new Finder(clock.transform);
            ((ISignalListener)rig.Agent).Hear(new Signal(CallForHelpTask.SIGNAL, Vector3.zero, null, clock.transform));
            var night = new TimeSensorTask { StoreIn = CLOCK };
            var listening = new DetectionTask(new CallsForHelpSensorTask())
            {
                StoreIn = "Heard", Tag = night.Tag, Interval = 0f
            };

            Step(rig, night);
            Step(rig, listening);

            Assert.IsNull(night.SeenBy(rig.Context), "A call for help is about an enemy, not about the hour");
            Assert.AreSame(clock.transform, listening.SeenBy(rig.Context),
                "Calls For Help wired into a Detection of the clock's kind would have taken it");
        }

        // ---------- Patrol Area ----------

        [Test]
        public void PatrolArea_AroundAnObjectThatDrawsNothing_EveryPlaceWithinRadius_NeverLeaves_NeverDone()
        {
            var rig = new GraphRig(trace: false);
            Transform area = NewObject("Meadow", new Vector3(10f, 0f, 4f));
            rig.Context.Blackboard.Set(AREA, area);
            rig.Actuator.Position = area.position;
            var patrol = new PatrolAreaTask { Area = AREA, Radius = 3f, Pause = 0.1f, PauseMax = 0.3f };
            float radius = patrol.Radius.Get(null);

            PatrolLog log = Patrol(rig, patrol, 40f);

            Assert.IsTrue(log.AlwaysRunning, "It never finishes: only an arrow leads out of it");
            Assert.GreaterOrEqual(log.Places.Count, 8, "It walks from place to place");
            Assert.That(log.Places.Max(place => Flat(place - area.position).magnitude),
                Is.LessThanOrEqualTo(radius + 1e-4f), "Every place within Radius of the area object");
            Assert.That(log.Path.Max(at => Flat(at - area.position).magnitude), Is.LessThanOrEqualTo(radius + 1e-3f),
                "...and so is every step of the way");
        }

        [Test]
        public void PatrolArea_PausesBetweenPauseAndPauseMax_TheSameSeedWalksTheSameWay()
        {
            Transform area = NewObject("Yard", new Vector3(-6f, 0f, 2f));
            var patrol = new PatrolAreaTask { Area = AREA, Radius = 3f, Pause = 0.2f, PauseMax = 1f };
            float pause = patrol.Pause.Get(null);
            float pauseMax = patrol.PauseMax.Get(null);
            var logs = new List<PatrolLog>();
            for (int i = 0; i < 2; i++)
            {
                var rig = new GraphRig(trace: false);           // the same seed both times
                rig.Context.Blackboard.Set(AREA, area);
                rig.Actuator.Position = area.position;
                logs.Add(Patrol(rig, patrol, 30f));
            }

            List<int> stops = logs[0].Pauses;
            Assert.GreaterOrEqual(stops.Count, 5, "It stopped at place after place");
            Assert.That(stops.Select(steps => steps * DT), Is.All.InRange(pause, pauseMax + 2 * DT + 1e-4f),
                "Every stop between Pause and PauseMax");
            Assert.Greater(stops.Distinct().Count(), 1, "...at random, not like a clock");
            CollectionAssert.AreEqual(logs[0].Places, logs[1].Places, "The same seed: the same places");
            CollectionAssert.AreEqual(stops, logs[1].Pauses, "...and the same stops");
        }

        [Test]
        public void PatrolArea_WalksAtItsPace_FullSpeedAgainWhenTakenAway()
        {
            var rig = new GraphRig(trace: false);
            var walker = new Walker(rig.Actuator);
            rig.Context.Navigation = walker;
            var patrol = new PatrolAreaTask { Radius = 3f, Pause = 0f, PauseMax = 0.1f, Pace = 0.3f };

            PatrolLog log = Patrol(rig, patrol, 6f);

            Assert.GreaterOrEqual(log.Places.Count, 2, "It walked from place to place");
            CollectionAssert.IsNotEmpty(walker.Paces);
            Assert.That(walker.Paces, Is.All.EqualTo(patrol.Pace.Get(null)), "A patrol at its Pace: a stroll, not a run");

            patrol.Interrupt(rig.Context);

            Assert.AreEqual(1f, walker.Pace, "Full speed again for whatever comes next (a chase)");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PatrolArea_NoAreaObject_WandersWithinRadiusOfWhereItBegan(bool namedButEmpty)
        {
            var rig = new GraphRig(trace: false);
            var start = new Vector3(-4f, 0f, 7f);
            rig.Actuator.Position = start;
            var patrol = new PatrolAreaTask { Radius = 2.5f, Pause = 0f, PauseMax = 0.2f };
            if (namedButEmpty)
            {
                patrol.Area = AREA;
                rig.Context.Blackboard.Set<Transform>(AREA, null);
            }

            float radius = patrol.Radius.Get(null);

            PatrolLog log = Patrol(rig, patrol, 20f);

            Assert.IsTrue(log.AlwaysRunning);
            Assert.GreaterOrEqual(log.Places.Count, 5, "It wanders all the same");
            Assert.That(log.Places.Max(place => Flat(place - start).magnitude), Is.LessThanOrEqualTo(radius + 1e-4f),
                "Every place within Radius of where the patrol began");
            Assert.That(log.Path.Max(at => Flat(at - start).magnitude), Is.LessThanOrEqualTo(radius + 1e-3f));
        }

        [Test]
        public void PatrolArea_AnObjectThatDraws_PlacesOnItsBoxSeenFromAbove_NotInTheRadius()
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.hideFlags = HideFlags.HideAndDontSave;
            _created.Add(box);
            box.transform.position = new Vector3(20f, 0f, 0f);
            box.transform.localScale = new Vector3(6f, 1f, 2f);
            Bounds bounds = box.GetComponent<Renderer>().bounds;
            Assume.That(bounds.size.x, Is.EqualTo(6f).Within(1e-3f), "The box is drawn at its size");
            var rig = new GraphRig(trace: false);
            rig.Context.Blackboard.Set(AREA, box.transform);
            rig.Actuator.Position = box.transform.position;
            var patrol = new PatrolAreaTask { Area = AREA, Radius = 1f, Pause = 0f, PauseMax = 0.1f };

            PatrolLog log = Patrol(rig, patrol, 30f);

            Assert.GreaterOrEqual(log.Places.Count, 8);
            foreach (Vector3 place in log.Places)
            {
                Assert.That(place.x, Is.InRange(bounds.min.x - 1e-4f, bounds.max.x + 1e-4f), "On the box");
                Assert.That(place.z, Is.InRange(bounds.min.z - 1e-4f, bounds.max.z + 1e-4f), "On the box");
            }

            Assert.Greater(log.Places.Max(place => Flat(place - box.transform.position).magnitude),
                patrol.Radius.Get(null), "The box the object draws decides, not the Radius");
        }

        [Test]
        public void PatrolArea_APlaceWithNoWayToIt_IsDropped_AnotherIsPicked()
        {
            var rig = new GraphRig(trace: false);
            Transform area = NewObject("Riverbank", Vector3.zero);
            var walker = new Walker(rig.Actuator) { CanReach = point => point.x <= area.position.x };  // no way east
            rig.Context.Navigation = walker;
            rig.Context.Blackboard.Set(AREA, area);
            var patrol = new PatrolAreaTask { Area = AREA, Radius = 4f, Pause = 0f, PauseMax = 0.1f };
            var places = new List<Vector3>();
            int mostAsks = 0;

            for (int i = 0; i < Steps(20f); i++)
            {
                int asked = walker.Asked;
                Assert.AreEqual(TaskStatus.Running, Step(rig, patrol));
                mostAsks = Mathf.Max(mostAsks, walker.Asked - asked);
                Vector3? place = patrol.CurrentPointFor(rig.Context);
                if (place.HasValue && !places.Contains(place.Value))
                    places.Add(place.Value);
            }

            Assert.Greater(walker.Refused, 0, "Places with no way to them came up");
            Assert.GreaterOrEqual(places.Count, 5, "...and others were taken instead");
            Assert.That(places.Max(place => place.x), Is.LessThanOrEqualTo(area.position.x),
                "Never a place it has no way to");
            Assert.AreEqual(1, mostAsks, "One place tried per step: a bad one costs a step, not a loop");
        }

        [TestCase(0f)]
        [TestCase(-2f)]
        public void PatrolArea_NoRoomToWander_EveryPlaceIsTheMiddle(float radius)
        {
            var rig = new GraphRig(trace: false);
            Transform area = NewObject("Post", new Vector3(3f, 0f, -3f));
            rig.Context.Blackboard.Set(AREA, area);
            var patrol = new PatrolAreaTask { Area = AREA, Radius = radius, Pause = 0.1f, PauseMax = 0.2f };

            PatrolLog log = Patrol(rig, patrol, 10f);

            Assert.IsTrue(log.AlwaysRunning);
            Assert.IsNotEmpty(log.Places);
            Assert.That(log.Places.Max(place => Flat(place - area.position).magnitude), Is.LessThan(1e-4f),
                "Every place is the middle of the area");
            Assert.LessOrEqual(Flat(rig.Actuator.Position - area.position).magnitude,
                patrol.ArriveDistance.Get(null) + 1e-4f, "It went there and stays");
        }

        [Test]
        public void PatrolArea_NoWayToWalk_Fails()
        {
            var rig = new GraphRig(trace: false);
            rig.Context.Navigation = null;

            Assert.AreEqual(TaskStatus.Failure, new PatrolAreaTask().Tick(rig.Context, DT), "No navigator");
            Assert.AreEqual(TaskStatus.Failure, new PatrolAreaTask().Tick(null, DT), "No agent");
        }

        // ---------- Patrol ----------

        [Test]
        public void Patrol_NoRoute_PacesIdleRangeAroundWhereItFirstStarted()
        {
            var rig = new GraphRig(trace: false);
            var start = new Vector3(3f, 0f, -2f);
            rig.Actuator.Position = start;
            var patrol = new PatrolTask { IdleRange = 4f, PauseAtPoint = 0.1f };
            Vector3 half = Vector3.right * (patrol.IdleRange.Get(null) * 0.5f);
            Vector3[] pacing = { start + half, start - half };

            bool Paces(Vector3 goal) => pacing.Any(point => (point - goal).sqrMagnitude < 1e-8f);

            List<Vector3> goals = Destinations(rig, patrol, 8f);
            Assert.AreEqual(2, goals.Count, "Back and forth between two points");
            foreach (Vector3 goal in goals)
                Assert.IsTrue(Paces(goal), $"{goal}: half the IdleRange to each side of the start");

            patrol.Interrupt(rig.Context);                      // it leaves the node...
            rig.Actuator.Position = new Vector3(10f, 0f, 10f);  // ...and comes back to it far away

            goals = Destinations(rig, patrol, 8f);
            CollectionAssert.IsNotEmpty(goals);
            foreach (Vector3 goal in goals)
                Assert.IsTrue(Paces(goal), $"{goal}: still around where it first started");
        }

        [Test]
        public void Patrol_PausesAtEachPoint_RandomBetweenPauseAndPauseMax_TheSameSeedTheSamePauses()
        {
            PatrolTask NewPatrol() =>
                new(Vector3.zero, new Vector3(4f, 0f, 0f)) { PauseAtPoint = 0.3f, PauseAtPointMax = 0.9f };

            PatrolTask patrol = NewPatrol();
            float pause = patrol.PauseAtPoint.Get(null);
            float pauseMax = patrol.PauseAtPointMax.Get(null);

            List<int> stops = Stops(new GraphRig(trace: false), patrol, 20f);
            List<int> again = Stops(new GraphRig(trace: false), NewPatrol(), 20f);

            Assert.GreaterOrEqual(stops.Count, 5, "It went back and forth");
            Assert.That(stops.Select(steps => steps * DT), Is.All.InRange(pause, pauseMax + 2 * DT + 1e-4f),
                "Stood still between PauseAtPoint and PauseAtPointMax at each point");
            Assert.Greater(stops.Distinct().Count(), 1, "...at random");
            CollectionAssert.AreEqual(stops, again, "The same seed: the same pauses");
        }

        [Test]
        public void Patrol_PauseMaxNotAbovePause_EveryPauseIsPause()
        {
            var patrol = new PatrolTask(Vector3.zero, new Vector3(3f, 0f, 0f))
            {
                PauseAtPoint = 0.5f, PauseAtPointMax = 0.2f
            };
            float pause = patrol.PauseAtPoint.Get(null);

            List<int> stops = Stops(new GraphRig(trace: false), patrol, 15f);

            Assert.GreaterOrEqual(stops.Count, 4);
            Assert.AreEqual(1, stops.Distinct().Count(), "Every pause the same");
            Assert.That(stops[0] * DT, Is.InRange(pause, pause + 2 * DT + 1e-4f));
        }

        [Test]
        public void Patrol_PausingWhileAStrongerClaimHoldsTheBody_PressesNothing()
        {
            var rig = new GraphRig(trace: false);
            var patrol = new PatrolTask(Vector3.zero, new Vector3(4f, 0f, 0f)) { PauseAtPoint = 1f };
            Step(rig, patrol);                                  // at its first point: it stands there for a while
            var ability = new object();
            Assume.That(rig.Context.Arbiter.TryClaim(ability, BodyAxis.Move, MoveAuthority.Frozen),
                "An ability holds the body");
            Vector3 before = rig.Actuator.Position;
            const int steps = 10;

            for (int i = 0; i < steps; i++)
            {
                rig.Actuator.Move(Vector3.forward);             // what the ability that holds the body presses
                Step(rig, patrol);
            }

            Assert.AreEqual(steps * rig.Actuator.Speed * DT, rig.Actuator.Position.z - before.z, 1e-4f,
                "One owner per axis (handbook §15): the pause writes nothing while a stronger claim holds the body");
        }

        // ---------- Wait ----------

        [Test]
        public void Wait_Seconds_DoneAfterThatLong()
        {
            var rig = new GraphRig(trace: false);
            var wait = new WaitTask(0.5f);
            float seconds = wait.Seconds.Get(null);

            int steps = StepsToEnd(rig, wait, seconds + 1f, out TaskStatus status);

            Assert.AreEqual(TaskStatus.Success, status, "Done");
            Assert.That(steps * DT, Is.InRange(seconds - 1e-4f, seconds + DT + 1e-4f));
        }

        [Test]
        public void Wait_SecondsMaxAbove_RandomBetween_NewOnEveryEntry_TheSameSeedTheSamePause()
        {
            var wait = new WaitTask(1f, 3f);
            float seconds = wait.Seconds.Get(null);
            float secondsMax = wait.SecondsMax.Get(null);

            List<int> pauses = Pauses(new GraphRig(trace: false), wait, 12);
            List<int> again = Pauses(new GraphRig(trace: false), wait, 12);     // another agent, the same seed

            Assert.That(pauses.Select(steps => steps * DT), Is.All.InRange(seconds - 1e-4f, secondsMax + DT + 1e-4f),
                "Between Seconds and SecondsMax");
            Assert.Greater(pauses.Distinct().Count(), 1, "A new pause on every entry");
            CollectionAssert.AreEqual(pauses, again, "The same seed: the same pauses");
        }

        [Test]
        public void Wait_SecondsMaxNotAbove_OrNoAgentToDrawFrom_ExactlySeconds()
        {
            var rig = new GraphRig(trace: false);
            foreach (WaitTask wait in new[] { new WaitTask(0.4f, 0.4f), new WaitTask(0.4f, 0.1f) })
            {
                float seconds = wait.Seconds.Get(null);
                List<int> pauses = Pauses(rig, wait, 4);
                Assert.AreEqual(1, pauses.Distinct().Count(), "Never at random");
                Assert.That(pauses[0] * DT, Is.InRange(seconds - 1e-4f, seconds + DT + 1e-4f), "Seconds");
            }

            var unbound = new WaitTask(0.4f, 3f);
            float exactly = unbound.Seconds.Get(null);
            int steps = 0;
            TaskStatus status = TaskStatus.Running;
            while (status == TaskStatus.Running && steps < Steps(5f))
            {
                status = unbound.Tick(null, DT);
                steps++;
            }

            Assert.AreEqual(TaskStatus.Success, status);
            Assert.That(steps * DT, Is.InRange(exactly - 1e-4f, exactly + DT + 1e-4f),
                "No agent, no random to draw from: Seconds");
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        public void Wait_ZeroOrNegativeSeconds_DoneOnTheFirstStep(float seconds)
        {
            var rig = new GraphRig(trace: false);

            Assert.AreEqual(TaskStatus.Success, new WaitTask(seconds).Tick(rig.Context, DT));
        }

        // ---------- stepping ----------

        private static int Steps(float seconds) => Mathf.CeilToInt(seconds / DT - 1e-3f);

        // One step of a task ticked by hand, in the agent's order: its clock, the task, then the body moves
        private static TaskStatus Step(GraphRig rig, AgentTask task)
        {
            rig.Context.Advance(DT);
            TaskStatus status = task.Tick(rig.Context, DT);
            (rig.Context.Actions.View as CharacterActionsWatch)?.Read();      // the presentation looks, as every frame
            rig.Actuator.Apply(DT);
            return status;
        }

        private static void Run(GraphRig rig, AgentTask task, float seconds)
        {
            for (int i = 0; i < Steps(seconds); i++)
                Step(rig, task);
        }

        // False — the task finished on the way
        private static bool KeepsRunning(GraphRig rig, AgentTask task, float seconds)
        {
            for (int i = 0; i < Steps(seconds); i++)
            {
                if (Step(rig, task) != TaskStatus.Running)
                    return false;
            }

            return true;
        }

        // The steps until the task finished (that step included)
        private static int StepsToEnd(GraphRig rig, AgentTask task, float maxSeconds, out TaskStatus status)
        {
            status = TaskStatus.Running;
            int steps = 0;
            while (status == TaskStatus.Running && steps < Steps(maxSeconds))
            {
                status = Step(rig, task);
                steps++;
            }

            return steps;
        }

        // The steps until the condition held after a step (that step included); -1 — it never did
        private static int StepsUntil(GraphRig rig, AgentTask task, Func<bool> condition, float maxSeconds)
        {
            for (int i = 1; i <= Steps(maxSeconds); i++)
            {
                Step(rig, task);
                if (condition())
                    return i;
            }

            return -1;
        }

        // A talk of several agents: every step their world opens its step first (what its runner does every fixed
        // step), then each one still talking steps. What each one came to
        private static TaskStatus[] Talk(ChatTask chat, GraphRig[] rigs, float maxSeconds,
            Action<int, TaskStatus[]> afterStep = null)
        {
            var status = new TaskStatus[rigs.Length];
            for (int step = 0; step < Steps(maxSeconds) && status.Contains(TaskStatus.Running); step++)
            {
                rigs.Select(rig => rig.Agent.World).FirstOrDefault(world => world != null)?.BeginStep(DT);
                for (int i = 0; i < rigs.Length; i++)
                {
                    if (status[i] == TaskStatus.Running)
                        status[i] = Step(rigs[i], chat);
                }

                afterStep?.Invoke(step, status);
            }

            return status;
        }

        private sealed class PatrolLog
        {
            /// <summary>Every place it set out for, in order.</summary>
            public readonly List<Vector3> Places = new();

            /// <summary>The steps of every stop between two places (with no place to walk to).</summary>
            public readonly List<int> Pauses = new();

            /// <summary>Where the body was after every step.</summary>
            public readonly List<Vector3> Path = new();

            public bool AlwaysRunning = true;
        }

        private static PatrolLog Patrol(GraphRig rig, PatrolAreaTask patrol, float seconds)
        {
            var log = new PatrolLog();
            bool walking = false;
            int stop = 0;
            for (int i = 0; i < Steps(seconds); i++)
            {
                log.AlwaysRunning &= Step(rig, patrol) == TaskStatus.Running;
                log.Path.Add(rig.Actuator.Position);
                Vector3? place = patrol.CurrentPointFor(rig.Context);
                if (!place.HasValue)
                {
                    walking = false;
                    stop++;
                    continue;
                }

                if (!walking)
                {
                    log.Places.Add(place.Value);
                    if (stop > 0)
                        log.Pauses.Add(stop);
                }

                walking = true;
                stop = 0;
            }

            return log;
        }

        // The steps of every stop the body made (it did not move), a stop still going at the end left out
        private static List<int> Stops(GraphRig rig, AgentTask task, float seconds)
        {
            var stops = new List<int>();
            int stop = 0;
            for (int i = 0; i < Steps(seconds); i++)
            {
                Vector3 before = rig.Actuator.Position;
                Step(rig, task);
                if ((rig.Actuator.Position - before).sqrMagnitude < 1e-12f)
                {
                    stop++;
                    continue;
                }

                if (stop > 0)
                    stops.Add(stop);
                stop = 0;
            }

            return stops;
        }

        // Every point the navigator was sent to, each once
        private static List<Vector3> Destinations(GraphRig rig, AgentTask task, float seconds)
        {
            var goals = new List<Vector3>();
            for (int i = 0; i < Steps(seconds); i++)
            {
                Step(rig, task);
                Vector3 goal = rig.Context.Navigation.Destination;
                if (!goals.Any(seen => (seen - goal).sqrMagnitude < 1e-8f))
                    goals.Add(goal);
            }

            return goals;
        }

        // The steps of each run of a wait, entered anew every time
        private static List<int> Pauses(GraphRig rig, AgentTask wait, int count)
        {
            var pauses = new List<int>();
            for (int i = 0; i < count; i++)
                pauses.Add(StepsToEnd(rig, wait, 10f, out _));

            return pauses;
        }

        // ---------- the world around ----------

        private Transform NewObject(string name, Vector3 position, string tag = null)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            if (tag != null)
                go.tag = tag;
            go.transform.position = position;
            return go.transform;
        }

        // What the sensors look for by default ("Player")
        private Transform Enemy(Vector3 position) => NewObject("Enemy", position, new RadiusSensorTask().Tag);

        // A clock of the scene, of the kind the time sensors look for unless another tag is given
        private Clock NewClock(float hour, string tag = null)
        {
            Transform clock = NewObject("Day Cycle", Vector3.zero, tag ?? new TimeSensorTask().Tag);
            var component = clock.gameObject.AddComponent<Clock>();
            component.Hour = hour;
            return component;
        }

        private static GraphRig Woken(GraphRig rig, Vector3 at)
        {
            rig.Agent.gameObject.SetActive(true);               // awake: the roster and the bus count it
            rig.Actuator.Position = at;
            return rig;
        }

        private static GraphRig RigIn(AIWorld world, Vector3 at) => Woken(new GraphRig(trace: false, world: world), at);

        // An awake agent on the bus: it hears the others and shouts to them
        private static GraphRig Listening(SignalBus bus, Vector3 at)
        {
            GraphRig rig = Woken(new GraphRig(trace: false), at);
            rig.Agent.SetSignals(bus);
            return rig;
        }

        // The sensor writes the Brain's Target: Watch (1) → a long Wait "Help" while it sees something; Daily Routine (2) →
        // a long Wait "Idle"
        private static BehaviorGraph HelpGraph(SensorTask sensor)
        {
            var g = new BrainGraphBuilder();
            g.Sensor(sensor);
            g.BrainTask.Target = sensor.StoreIn;
            g.Variable(sensor.StoreIn, VariableType.Transform);
            g.Do(g.Decision(new WatchReasoner(), 1), g.Block(new WaitTask(1000f), "Help"));
            g.Do(g.Always(), g.Block(new WaitTask(1000f), "Idle"));
            return g.Build();
        }

        // The Time Of Day sensor writes its variable; Sleep (1, its Night pin wired to the variable) → a long Wait "Night";
        // Daily Routine (2) → a long Wait "Day"
        private static BehaviorGraph DayGraph(TimeSensorTask sensor)
        {
            var g = new BrainGraphBuilder();
            g.Sensor(sensor);
            g.Variable(sensor.StoreIn, VariableType.Transform);
            var sleep = new SleepReasoner { Condition = new CompareCondition(sensor.StoreIn, VariableType.Transform) };
            g.Do(g.Decision(sleep, 1), g.Block(new WaitTask(1000f), "Night"));
            g.Do(g.Always(), g.Block(new WaitTask(1000f), "Day"));
            return g.Build();
        }

        // Whether the decision holds now, asked as the Brain asks it (no target)
        private static bool Holds(ReasonerTask decision, AgentContext context, out string whyNot) =>
            decision.Holds(new ReasonerQuery(context, TargetFacts.None, false), out whyNot);

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        // A number a node keeps to itself (not a setting of the canvas): read from the node, so the test follows it
        private static float Constant<TNode>(string name)
        {
            FieldInfo field = typeof(TNode).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, $"{typeof(TNode).Name}.{name} is gone: the test reads it");
            return Convert.ToSingle(field.GetRawConstantValue());
        }

        // ---------- fakes ----------

        /// <summary>
        /// A navigator as a test needs it: straight at the point, at the pace it is given; a way or none (CanReach),
        /// stuck in front of something (Blocked), or giving up on the way (GiveUp: Unreachable until cleared, as a
        /// NavMesh path that turned out partial).
        /// </summary>
        private sealed class Walker : INavigator, INavigationPace
        {
            private readonly TestActuator _body;
            private NavStatus _status;
            private bool _gaveUp;

            public Walker(TestActuator body) => _body = body;

            public Func<Vector3, bool> CanReach = _ => true;
            public bool Blocked;

            /// <summary>The pace of every step walked.</summary>
            public readonly List<float> Paces = new();

            /// <summary>How many destinations it was given, and how many of them had no way to them.</summary>
            public int Asked;
            public int Refused;

            public float Pace { get; set; } = 1f;

            public NavStatus Status => _gaveUp ? NavStatus.Unreachable : _status;

            public Vector3 Destination { get; private set; }

            public Vector3 SteeringTarget => Destination;

            public float RemainingDistance =>
                Status.IsMoving() ? Vector3.Distance(_body.Position, Destination) : Mathf.Infinity;

            public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

            public void GiveUp() => _gaveUp = true;

            public NavVerdict SetDestination(Vector3 point)
            {
                Asked++;
                Destination = point;
                if (!CanReach(point))
                {
                    Refused++;
                    return NavVerdict.Unreachable;
                }

                if (!_status.IsMoving())
                    _status = NavStatus.Following;
                return NavVerdict.Reachable;
            }

            public void Clear()
            {
                _status = NavStatus.Idle;
                _gaveUp = false;
            }

            public void Step(float dt)
            {
                if (!Status.IsMoving())
                    return;

                Paces.Add(Pace);
                if (Blocked)
                {
                    _status = NavStatus.Blocked;
                    _body.Move(Vector3.zero);
                    return;
                }

                _body.Move(Flat(Destination - _body.Position), Pace);
            }
        }

        /// <summary>A scene's clock: the hour the time sensors read.</summary>
        private sealed class Clock : MonoBehaviour, IDayClock
        {
            public float Hour { get; set; }
        }

        /// <summary>The objects of a "scene" a sensor may find, by their tag: what it sees without physics.</summary>
        private sealed class Finder : IObjectFinder
        {
            private readonly List<Transform> _objects;

            public Finder(params Transform[] objects) => _objects = objects.ToList();

            public Transform FindNearest(string kind, Vector3 center, float radius, Transform self)
            {
                var found = new List<Transform>();
                FindAll(kind, center, radius, self, found);
                return found.OrderBy(candidate => (candidate.position - center).sqrMagnitude).FirstOrDefault();
            }

            public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
            {
                results.Clear();
                bool anywhere = radius >= float.MaxValue * 0.5f;
                foreach (Transform candidate in _objects)
                {
                    if (candidate == null || candidate == self || !candidate.gameObject.activeInHierarchy ||
                        candidate.tag != kind)
                        continue;

                    if (anywhere || (candidate.position - center).sqrMagnitude <= radius * radius)
                        results.Add(candidate);
                }
            }
        }
    }
}
