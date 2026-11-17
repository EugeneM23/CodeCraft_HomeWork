using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// A villager's day in the world (step 89; the canvas is the program — going in is a block, not a component that
    /// hides the character): Go Inside walks to the door at its pace, goes in at Arrive Distance (or next to the door
    /// after Stuck Time when somebody stands on it), stays Duration — 0: until taken away — and comes out on every way out;
    /// no door or no way to it fails at once, a door never reached fails at its walk limit. Going in hides only what was
    /// drawn and brings back exactly that. A talk's next comer takes the seat farthest from those seated.
    /// </summary>
    public sealed class WorldLifeTests
    {
        private const string DOOR = "Door";
        private const string PLACE = "Place";
        private const float DT = 0.02f;

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

        // ---------- Go Inside ----------

        [Test]
        public void GoInside_WalksToTheDoorAtItsPace_GoesIn_ComesOutAfterItsDuration()
        {
            GraphRig rig = Rig(Vector3.zero, out WorldWalker walker);
            Transform door = Thing(DOOR, new Vector3(6f, 0f, 0f));
            rig.Context.Blackboard.Set(DOOR, door);
            var goInside = new GoInsideTask { Place = DOOR, Duration = 2f, Pace = 0.4f };

            TaskStatus answer = TaskStatus.Running;
            float time = 0f;
            float wentInAt = -1f;
            Vector3 wentInHere = default;
            bool stayedIn = true;
            for (int i = 0; i < 1000 && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = goInside.Tick(rig.Context, DT);
                time += DT;
                if (wentInAt < 0f && rig.Agent.IsInside)
                {
                    wentInAt = time;
                    wentInHere = rig.Actuator.Position;
                    Assert.That(Flat(wentInHere - door.position).magnitude,
                        Is.LessThanOrEqualTo(goInside.ArriveDistance.Get(rig.Context)), "In at Arrive Distance of the door");
                }
                else if (wentInAt > 0f && answer == TaskStatus.Running)
                {
                    stayedIn &= rig.Agent.IsInside && goInside.IsInsideFor(rig.Context);
                }

                rig.Actuator.Apply(DT);
            }

            Assert.IsNotEmpty(walker.Paces, "Set-up: it walked");
            Assert.That(walker.Paces, Is.All.EqualTo(0.4f), "To the door at its pace");
            Assert.Greater(wentInAt, 0f, "It went in");
            Assert.IsTrue(stayedIn, "…and stayed in");
            Assert.AreEqual(TaskStatus.Success, answer, "Came out: Done");
            Assert.That(time - wentInAt, Is.EqualTo(goInside.Duration.Get(rig.Context)).Within(2f * DT),
                "…Duration after it went in");
            Assert.IsFalse(rig.Agent.IsInside, "Out again");
            Assert.That((rig.Actuator.Position - wentInHere).magnitude, Is.LessThan(1e-4f), "Where it went in");
            Assert.AreEqual(1f, walker.Pace, "The full pace again");
        }

        [Test]
        public void GoInside_DurationZero_StaysInsideUntilTakenAway_ComesOutOnEveryWayOut()
        {
            GraphRig rig = Rig(new Vector3(5f, 0f, 0f), out WorldWalker walker);
            Transform door = Thing(DOOR, new Vector3(5.1f, 0f, 0f));
            rig.Context.Blackboard.Set(DOOR, door);
            var goInside = new GoInsideTask { Place = DOOR, Duration = 0f };
            Assert.AreEqual(TaskStatus.Running, Run(rig, goInside, 0.1f), "Set-up: in at once (at the door)");
            Assert.IsTrue(rig.Agent.IsInside, "Set-up: inside");
            Vector3 inside = rig.Actuator.Position;

            Assert.AreEqual(TaskStatus.Running, Run(rig, goInside, 30f), "Duration 0: it stays inside");
            Assert.IsTrue(rig.Agent.IsInside);
            Assert.That((rig.Actuator.Position - inside).magnitude, Is.LessThan(1e-4f), "…not moving");

            goInside.Interrupt(rig.Context);
            Assert.IsFalse(rig.Agent.IsInside, "Taken away (the morning came): out again");
            Assert.AreEqual(NavStatus.Idle, walker.Status, "…the walk called off");

            Assert.AreEqual(TaskStatus.Running, Run(rig, goInside, 0.1f), "Set-up: in again");
            Assert.IsTrue(rig.Agent.IsInside);
            rig.Context.Blackboard.Set(DOOR, (Transform)null);
            Assert.AreEqual(TaskStatus.Failure, goInside.Tick(rig.Context, DT), "The door is gone: Failed");
            Assert.IsFalse(rig.Agent.IsInside, "…and it is out again");
        }

        [Test]
        public void GoInside_SomebodyAtTheDoor_GoesInAfterTheStuckTime_NoDoorOrNoWay_Failed()
        {
            float stuckTime = Constant<GoInsideTask>("STUCK_TIME");        // the node's own numbers: the test follows them
            float walkLimit = Constant<GoInsideTask>("WALK_LIMIT");
            GraphRig rig = Rig(Vector3.zero, out WorldWalker walker);
            walker.Blocked = true;                                          // somebody stands on the door
            Transform door = Thing(DOOR, new Vector3(0.6f, 0f, 0f));        // within four times Arrive Distance
            rig.Context.Blackboard.Set(DOOR, door);
            var goInside = new GoInsideTask { Place = DOOR, Duration = 5f };

            float wentInAt = -1f;
            float time = 0f;
            for (int i = 0; i < 200 && wentInAt < 0f; i++)
            {
                rig.Context.Advance(DT);
                goInside.Tick(rig.Context, DT);
                time += DT;
                if (rig.Agent.IsInside)
                    wentInAt = time;
                rig.Actuator.Apply(DT);
            }

            Assert.That(wentInAt, Is.EqualTo(stuckTime).Within(2f * DT), "In from next to the door after the stuck time");
            goInside.Interrupt(rig.Context);

            GraphRig noDoor = Rig(Vector3.zero, out _);
            Assert.AreEqual(TaskStatus.Failure, new GoInsideTask { Place = DOOR }.Tick(noDoor.Context, DT),
                "No door on the blackboard: Failed at once");

            GraphRig noWay = Rig(Vector3.zero, out WorldWalker nowhere);
            nowhere.CanReach = _ => false;
            noWay.Context.Blackboard.Set(DOOR, Thing("Far Door", new Vector3(10f, 0f, 0f)));
            Assert.AreEqual(TaskStatus.Failure, new GoInsideTask { Place = DOOR }.Tick(noWay.Context, DT),
                "No way to the door: Failed at once");

            GraphRig stuck = Rig(Vector3.zero, out WorldWalker held);
            held.Blocked = true;
            stuck.Context.Blackboard.Set(DOOR, Thing("Distant Door", new Vector3(10f, 0f, 0f)));
            var never = new GoInsideTask { Place = DOOR };
            TaskStatus answer = TaskStatus.Running;
            time = 0f;
            for (int i = 0; i < 4000 && answer == TaskStatus.Running; i++)
            {
                stuck.Context.Advance(DT);
                answer = never.Tick(stuck.Context, DT);
                time += DT;
            }

            Assert.AreEqual(TaskStatus.Failure, answer, "Never reached: Failed");
            Assert.That(time, Is.EqualTo(walkLimit).Within(3f * DT), "…at its walk limit");
            Assert.IsFalse(stuck.Agent.IsInside);
        }

        [Test]
        public void IsInside_HidesOnlyWhatWasShown_BringsBackExactlyThat()
        {
            var rig = new GraphRig(trace: false);
            rig.Agent.gameObject.SetActive(true);                           // its parts are found (they are drawn)
            Renderer shown = Part(rig, "Shown");
            Renderer alsoShown = Part(rig, "Also Shown");
            Renderer hidden = Part(rig, "Hidden Before");
            hidden.enabled = false;                                         // a weapon on the back, say

            rig.Agent.IsInside = true;
            Assert.IsFalse(shown.enabled || alsoShown.enabled || hidden.enabled, "Inside: nothing of it is drawn");
            Assert.IsTrue(rig.Agent.TryGetState(CharacterState.Inside, out bool inside) && inside, "Status: Inside");

            rig.Agent.IsInside = true;
            Assert.IsFalse(shown.enabled || alsoShown.enabled, "In again: nothing changes");

            rig.Agent.IsInside = false;
            Assert.IsTrue(shown.enabled && alsoShown.enabled, "Out: what was drawn is drawn again");
            Assert.IsFalse(hidden.enabled, "…what was hidden before stays hidden");
            Assert.IsTrue(rig.Agent.TryGetState(CharacterState.Inside, out inside) && !inside, "Status: not Inside");
        }

        // ---------- a talk ----------

        [Test]
        public void Chat_TheNextOneTakesTheSeatFarthestFromThoseSeated()
        {
            const int SEATS = 3;
            const float RADIUS = 0.7f;
            var world = new AIWorld();
            Transform place = Thing("Well", Vector3.zero);                  // facing +z: seat 0 to its right
            Vector3 Seat(int i) => Quaternion.AngleAxis(i * 360f / SEATS, Vector3.up) * place.right * RADIUS;
            GraphRig first = Woken(world, Seat(0));
            GraphRig second = Woken(world, Seat(1));
            GraphRig third = Woken(world, new Vector3(0f, 0f, -2f));       // comes from the south
            Engagement circle = world.Roster.EngagementOf(place);
            circle.Tokens.HoldFirmly(ChatTask.POOL);
            circle.Tokens.SetCapacity(ChatTask.POOL, SEATS);
            circle.Tokens.Request(first.Agent, ChatTask.POOL, 1, 0f, 0f);
            circle.Tokens.Request(second.Agent, ChatTask.POOL, 1, 0f, 0f);
            world.BeginStep(DT);                                            // requests are granted at the world's step
            Assert.IsTrue(circle.Tokens.Holds(first.Agent, ChatTask.POOL) && circle.Tokens.Holds(second.Agent, ChatTask.POOL),
                "Set-up: two sit at seats 0 and 1");
            third.Context.Blackboard.Set(PLACE, place);
            var chat = new ChatTask { Place = PLACE, Seats = SEATS, Radius = RADIUS };

            for (int i = 0; i < 50 && !circle.Tokens.Holds(third.Agent, ChatTask.POOL); i++)
            {
                world.BeginStep(DT);
                third.Context.Advance(DT);
                Assert.AreEqual(TaskStatus.Running, chat.Tick(third.Context, DT));
            }

            Assert.IsTrue(circle.Tokens.Holds(third.Agent, ChatTask.POOL), "Set-up: it got a seat");
            Assert.That((third.Context.Navigation.Destination - Seat(2)).magnitude, Is.LessThan(1e-3f),
                "Seat 2 (240°): the one farthest from both who sit — not the nearer seat 1 behind it");
        }

        // ---------- helpers ----------

        private GraphRig Rig(Vector3 at, out WorldWalker walker)
        {
            var body = new TestActuator();
            walker = new WorldWalker(body);
            var rig = new GraphRig(trace: false, navigator: walker, actuator: body);
            rig.WakeUpAt(at);
            return rig;
        }

        // An agent of the world, awake (the roster counts it)
        private static GraphRig Woken(AIWorld world, Vector3 at)
        {
            var rig = new GraphRig(trace: false, world: world);
            rig.Agent.gameObject.SetActive(true);
            rig.WakeUpAt(at);
            return rig;
        }

        // A drawn part of the character (a cube's renderer under it)
        private Renderer Part(GraphRig rig, string name)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.hideFlags = HideFlags.HideAndDontSave;
            part.transform.SetParent(rig.Agent.transform, false);
            _created.Add(part);
            return part.GetComponent<Renderer>();
        }

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
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

        // A number a node keeps to itself (not a setting of the canvas): read from the node, so the test follows it
        private static float Constant<TNode>(string name)
        {
            FieldInfo field = typeof(TNode).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, $"{typeof(TNode).Name}.{name} is gone: the test reads it");
            return System.Convert.ToSingle(field.GetRawConstantValue());
        }
    }
}
