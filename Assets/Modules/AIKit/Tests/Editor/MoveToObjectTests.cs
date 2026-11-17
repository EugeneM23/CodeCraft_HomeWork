using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Move To an object of the world (step 90): the object says where the character stands — behind a cover, away from
    /// the Brain's target, the place going round it as the target moves — and how near is there (its reach); the claim on
    /// it is kept while the character walks; no way to it puts it into the character's unreachable journal. A plain Move
    /// To (a point, an object that is no object of the world) goes as it always went.
    /// </summary>
    public sealed class MoveToObjectTests
    {
        private const string OBJECT = "Object";
        private const float DT = 0.02f;

        private readonly List<Object> _created = new();
        private WorldObjects _registry;

        [SetUp]
        public void SetUp() => _registry = new WorldObjects();

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
        public void MoveTo_ACover_ItsPlaceBehindItAwayFromTheTarget_ReachedWithinTheCoversReach_ClaimKept()
        {
            GraphRig rig = Rig(Vector3.zero);
            Transform threat = Thing("Threat", new Vector3(0f, 0f, 14f));
            rig.Context.BrainTarget = threat;
            var cover = Thing("Crate", new Vector3(4f, 0f, 5f)).gameObject.AddComponent<Cover>();
            _registry.Register(cover);
            rig.Context.Blackboard.Set(OBJECT, cover.transform);
            Assert.IsTrue(_registry.TryClaim(cover, rig.Agent, 0.5f), "Set-up: found and claimed for half a second");
            var move = new MoveToTask { Target = OBJECT, Pace = 0.5f };

            float walked = 0f;
            TaskStatus answer = Run(rig, move, 10f, () =>
            {
                walked += DT;
                _registry.Step(DT);                                     // the world's clock: the claim would run out
            });

            Assert.AreEqual(TaskStatus.Success, answer, "Reached");
            Vector3 place = cover.PlaceFor(Offer.Occupy, rig.Context, threat);
            Assert.That(Flat(rig.Actuator.Position - place).magnitude, Is.LessThanOrEqualTo(cover.Reach + 0.1f),
                "At the place behind the cover, within its reach");
            Assert.Greater(Flat(rig.Actuator.Position - threat.position).magnitude,
                Flat(cover.transform.position - threat.position).magnitude, "The cover between it and the target");
            Assert.Greater(walked, 1f, "Set-up: a walk longer than the claim's own life");
            Assert.AreSame(rig.Agent, _registry.HolderOf(cover), "Still its: renewed while it walked");
        }

        [Test]
        public void MoveTo_AnObjectWithNoWayToIt_NoPath_ItIsUnreachableForTheCharacterAWhile()
        {
            GraphRig rig = Rig(Vector3.zero);
            rig.Context.Navigation = new NoWay();
            var rock = Thing("Rock", new Vector3(6f, 0f, 0f)).gameObject.AddComponent<PickableItem>();
            _registry.Register(rock);
            rig.Context.Blackboard.Set(OBJECT, rock.transform);

            Assert.AreEqual(TaskStatus.Failure, Run(rig, new MoveToTask { Target = OBJECT }, 1f), "No Path");
            Assert.IsTrue(_registry.IsUnreachableFor(rig.Agent, rock), "Into its unreachable journal");

            var find = new FindTask { What = new WhatValue { Offer = Offer.Take }, StoreIn = "Found" };
            Assert.AreEqual(TaskStatus.Failure, find.Tick(rig.Context, DT), "A search does not offer it again for a while");
        }

        [Test]
        public void MoveTo_APointOrAPlainObject_AsBefore()
        {
            GraphRig rig = Rig(Vector3.zero);
            Transform post = Thing("Post", new Vector3(5f, 0f, 0f));
            rig.Context.Blackboard.Set(OBJECT, post);

            Assert.AreEqual(TaskStatus.Success, Run(rig, new MoveToTask { Target = OBJECT, StopDistance = 0.3f }, 5f));
            Assert.That(Flat(rig.Actuator.Position - post.position).magnitude, Is.LessThanOrEqualTo(0.3f + 0.1f),
                "To the object itself, within Stop Distance");
            Assert.IsNull(new MoveToTask().Requirement, "A point: nothing to say");
            Assert.IsNotNull(new MoveToTask { Target = OBJECT }.Requirement, "An object: it says the place is the object's");
        }

        // ---------- helpers ----------

        /// <summary>A navigator that finds no way anywhere.</summary>
        private sealed class NoWay : INavigator
        {
            public NavStatus Status { get; private set; }

            public Vector3 Destination { get; private set; }

            public Vector3 SteeringTarget => Destination;

            public float RemainingDistance => Mathf.Infinity;

            public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

            public NavVerdict SetDestination(Vector3 point)
            {
                Destination = point;
                Status = NavStatus.Unreachable;
                return NavVerdict.Unreachable;
            }

            public void Clear() => Status = NavStatus.Idle;

            public void Step(float dt)
            {
            }
        }

        private GraphRig Rig(Vector3 at)
        {
            var rig = new GraphRig(trace: false);
            rig.WakeUpAt(at);
            rig.UseObjects(_registry);
            rig.UseWorldRack();
            return rig;
        }

        private Transform Thing(string name, Vector3 at)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            _created.Add(go);
            go.transform.position = at;
            return go.transform;
        }

        private static TaskStatus Run(GraphRig rig, AgentTask task, float seconds, System.Action eachTick = null)
        {
            TaskStatus answer = TaskStatus.Running;
            int steps = Mathf.CeilToInt(seconds / DT);
            for (int i = 0; i < steps && answer == TaskStatus.Running; i++)
            {
                rig.Context.Advance(DT);
                answer = task.Tick(rig.Context, DT);
                eachTick?.Invoke();
                rig.Actuator.Apply(DT);
            }

            return answer;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
