using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AIKit.Tests
{
    /// <summary>
    /// Stage 7: the module's own senses without a scene. Attention grows in sight and fades out of it, the alert ladder
    /// climbs and falls with hysteresis, memory gives search points, a shout raises a neighbour to Suspicious only, a
    /// hit wakes at once, looks are spread over time, and the core never touches physics.
    /// </summary>
    public class PerceptionTests
    {
        private const float DT = 0.02f;

        private readonly List<GraphRig> _rigs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (GraphRig rig in _rigs)
                rig.DestroyTarget();
            _rigs.Clear();
        }

        private sealed class FakeSight : ISightProbe
        {
            public bool Clear = true;

            public bool HasLineOfSight(Vector3 from, Vector3 to) => Clear;
        }

        private sealed class Perceivable : MonoBehaviour, IPerceivable
        {
            public bool Alive = true;
            public int Side;

            public bool IsPerceivable => Alive;

            public int Faction => Side;

            public Vector3 AimPoint => transform.position;
        }

        private GraphRig Rig(PerceptionProfile profile = null)
        {
            var rig = new GraphRig(trace: false);
            _rigs.Add(rig);
            rig.GivePerception(profile ?? PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
            }));
            return rig;
        }

        private static float SecondsUntil(GraphRig rig, Func<bool> condition, float max = 5f)
        {
            float time = 0f;
            while (!condition() && time < max)
            {
                rig.Step(DT);
                time += DT;
            }

            return time;
        }

        // ---------- attention ----------

        [Test]
        public void FullView_NoticesThenKnows_WithinTheProfilesTime()
        {
            GraphRig rig = Rig();
            rig.CreateTargetAt(new Vector3(5f, 0f, 0f));              // straight ahead, in the focus
            AgentPerception perception = rig.Context.Perception;

            float noticed = SecondsUntil(rig, () => perception.Level == AlertLevel.Suspicious);
            float knows = noticed + SecondsUntil(rig, () => perception.Level == AlertLevel.Aware);

            Assert.That(knows, Is.InRange(0.3f, 1.2f), "Full view: known in 0.3–1.2 s");
            Assert.Less(noticed, knows, "Suspicious first, then Aware");
            Assert.IsTrue(rig.Context.HasTarget, "The target is the agent's interest");
        }

        [Test]
        public void Periphery_NoticesSlowerThanTheFocus()
        {
            var profile = PerceptionProfile.Create(p => { p.VisionIntervalMin = 0f; p.VisionIntervalMax = 0f; });
            GraphRig ahead = Rig(profile);
            ahead.CreateTargetAt(new Vector3(5f, 0f, 0f));
            GraphRig aside = Rig(profile.Clone());
            aside.CreateTargetAt(new Vector3(1.5f, 4f, 0f));          // ~70° off the facing: periphery

            float inFocus = SecondsUntil(ahead, () => ahead.Context.Perception.Level == AlertLevel.Suspicious);
            float inPeriphery = SecondsUntil(aside, () => aside.Context.Perception.Level == AlertLevel.Suspicious);

            Assert.Greater(inPeriphery, inFocus * 1.5f);
        }

        [Test]
        public void OutOfSight_FadesThroughSearchingToIdle()
        {
            var profile = PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.MemoryTime = 1f;
                p.DecayPerSecond = 1f;
            });
            GraphRig rig = Rig(profile);
            var sight = new FakeSight();
            rig.Context.Sight = sight;
            rig.CreateTargetAt(new Vector3(5f, 0f, 0f));
            AgentPerception perception = rig.Context.Perception;
            SecondsUntil(rig, () => perception.Level == AlertLevel.Aware);

            sight.Clear = false;                                       // a wall comes between
            rig.Step(DT);

            Assert.AreEqual(AlertLevel.Searching, perception.Level, "Lost sight: searches");
            Assert.IsFalse(rig.Context.HasTarget, "Out of sight: only the place it was");
            Assert.AreEqual(new Vector3(5f, 0f, 0f), rig.Context.Track.LastKnownPosition);
            Assert.IsTrue(perception.HasMemory);

            SecondsUntil(rig, () => perception.Level == AlertLevel.Idle, 3f);
            Assert.AreEqual(AlertLevel.Idle, perception.Level, "Forgiven once the memory is gone");
            Assert.AreEqual(0f, perception.Awareness);
        }

        [Test]
        public void Aware_HeldWhileAwarenessStaysAboveNotice()
        {
            GraphRig rig = Rig();
            rig.CreateTargetAt(new Vector3(5f, 0f, 0f));
            AgentPerception perception = rig.Context.Perception;
            SecondsUntil(rig, () => perception.Level == AlertLevel.Aware);

            perception.Best.Awareness = 0.5f;                           // below "knows", above "notices"
            rig.Context.Perception.Profile.GainPerSecond = 0f;
            rig.Step(DT);

            Assert.AreEqual(AlertLevel.Aware, perception.Level, "Hysteresis: Aware holds down to the notice threshold");
        }

        [Test]
        public void SearchPoints_AroundTheLastPlace_SameSeedSamePoints()
        {
            List<Vector3> PointsOf(int seed)
            {
                var rig = new GraphRig(seed, trace: false);
                _rigs.Add(rig);
                rig.GivePerception(PerceptionProfile.Create(p =>
                {
                    p.VisionIntervalMin = 0f;
                    p.VisionIntervalMax = 0f;
                }));
                rig.Context.Perception.Alert(rig.CreateTargetAt(new Vector3(6f, 0f, 0f)));
                var points = new List<Vector3>();
                rig.Context.Perception.SearchPoints(4, 3f, points);
                return points;
            }

            List<Vector3> first = PointsOf(7);
            List<Vector3> again = PointsOf(7);

            Assert.AreEqual(4, first.Count);
            CollectionAssert.AreEqual(first, again, "One seed — the same places");
            Assert.IsTrue(first.All(p => Vector3.Distance(p, new Vector3(6f, 0f, 0f)) <= 3f + 1e-4f));
            Assert.IsTrue(first.All(p => Mathf.Abs(p.y) < 1e-4f), "On the body's ground plane");
        }

        [Test]
        public void TurnedGravity_ConesFollowTheBody()
        {
            var profile = PerceptionProfile.Create(p =>
            {
                p.VisionIntervalMin = 0f;
                p.VisionIntervalMax = 0f;
                p.FocusHalfAngle = 45f;
                p.PeripheryRange = 0f;
                p.BackRange = 1f;
            });
            GraphRig ahead = Rig(profile);
            ahead.Actuator.UpDirection = Vector3.right;               // standing on a wall: "ahead" is world down
            ahead.CreateTargetAt(new Vector3(0f, -5f, 0f));
            GraphRig behind = Rig(profile.Clone());
            behind.Actuator.UpDirection = Vector3.right;
            behind.CreateTargetAt(new Vector3(0f, 5f, 0f));

            ahead.Step(DT, 30);
            behind.Step(DT, 30);

            Assert.IsTrue(ahead.Context.HasTarget, "Ahead in the body's frame: seen");
            Assert.IsFalse(behind.Context.HasTarget, "Behind in the body's frame: not seen, wherever world up is");
        }

        // ---------- who counts ----------

        [Test]
        public void DeadOrSameSide_IsNoTarget()
        {
            GraphRig rig = Rig();
            rig.Agent.Faction = 2;                                    // the character's side (its Advanced section)
            Transform target = rig.CreateTargetAt(new Vector3(4f, 0f, 0f));
            var perceivable = target.gameObject.AddComponent<Perceivable>();

            perceivable.Side = 2;
            rig.Step(DT, 30);
            Assert.IsFalse(rig.Context.HasTarget, "Same side: not a target");

            perceivable.Side = 1;
            perceivable.Alive = false;
            rig.Step(DT, 30);
            Assert.IsFalse(rig.Context.HasTarget, "Dead: not a target");

            perceivable.Alive = true;
            SecondsUntil(rig, () => rig.Context.HasTarget);
            Assert.IsTrue(rig.Context.HasTarget, "Alive foe: seen");
        }

        // ---------- unfair inputs ----------

        [Test]
        public void Alert_FullAwarenessAtOnce_AndADecisionOnTheNextStep()
        {
            GraphRig rig = Rig();
            rig.SetGraph(GraphRig.OneState(new WaitTask(10f), "Idle"));
            rig.Step(DT);
            int decisions = rig.Agent.DecisionCount;
            Transform attacker = rig.CreateTargetAt(new Vector3(-3f, 0f, 0f));      // behind

            rig.Context.Perception.Alert(attacker);
            rig.Step(DT);

            Assert.AreEqual(1f, rig.Context.Track.Awareness);
            Assert.AreEqual(AlertLevel.Aware, rig.Context.Perception.Level);
            Assert.Greater(rig.Agent.DecisionCount, decisions, "Decided at once, not on the think interval");
        }

        [Test]
        public void Hear_AShout_RaisesToSuspiciousNotAware()
        {
            GraphRig listener = Rig();
            listener.Context.Sight = new FakeSight { Clear = false };   // it cannot see the subject itself
            Transform subject = new GameObject("Subject") { hideFlags = HideFlags.HideAndDontSave }.transform;
            subject.position = new Vector3(10f, 0f, 0f);
            try
            {
                listener.Context.Perception.Hear(new Signal("Alarm", new Vector3(9f, 0f, 0f), subject: subject));
                listener.Step(DT);

                Assert.AreEqual(AlertLevel.Suspicious, listener.Context.Perception.Level);
                Assert.Less(listener.Context.Perception.Awareness, listener.Context.Perception.Profile.AwareThreshold);
                Assert.AreEqual(new Vector3(9f, 0f, 0f), listener.Context.Track.LastKnownPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(subject.gameObject);
            }
        }

        // ---------- the bus ----------

        private sealed class Listener : ISignalListener
        {
            public Transform Root { get; set; }
            public Vector3 ListenPosition { get; set; }
            public int Faction { get; set; }
            public readonly List<string> Heard = new();
            public Action<ISignalListener> OnHear;

            public void Hear(in Signal signal)
            {
                Heard.Add(signal.Id);
                OnHear?.Invoke(this);
            }
        }

        [Test]
        public void Bus_Radius_Faction_AndNotTheSender()
        {
            var bus = new SignalBus();
            var near = new Listener { ListenPosition = new Vector3(3f, 0f, 0f), Faction = 1 };
            var far = new Listener { ListenPosition = new Vector3(30f, 0f, 0f), Faction = 1 };
            var other = new Listener { ListenPosition = new Vector3(2f, 0f, 0f), Faction = 2 };
            Transform sender = new GameObject("Sender") { hideFlags = HideFlags.HideAndDontSave }.transform;
            var self = new Listener { Root = sender, ListenPosition = Vector3.zero, Faction = 1 };
            try
            {
                foreach (Listener listener in new[] { near, far, other, self })
                    bus.Subscribe(listener);

                int heard = bus.Broadcast(new Signal("Alarm", Vector3.zero, sender), SignalArea.FactionWithin(1, 10f));

                Assert.AreEqual(1, heard);
                CollectionAssert.AreEqual(new[] { "Alarm" }, near.Heard);
                Assert.IsEmpty(far.Heard, "Beyond the radius");
                Assert.IsEmpty(other.Heard, "Another faction");
                Assert.IsEmpty(self.Heard, "The sender does not hear itself");
                Assert.AreEqual(3, bus.Broadcast(new Signal("Noise", Vector3.zero), SignalArea.Within(10f)),
                    "No sender: everyone within the radius, any faction");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sender.gameObject);
            }
        }

        [Test]
        public void Bus_UnsubscribingWhileHearing_IsSafe()
        {
            var bus = new SignalBus();
            var first = new Listener();
            var second = new Listener();
            first.OnHear = me =>
            {
                bus.Unsubscribe(me);
                bus.Unsubscribe(second);                            // and someone not yet reached
            };
            bus.Subscribe(first);
            bus.Subscribe(second);

            Assert.DoesNotThrow(() => bus.Broadcast(new Signal("Alarm", Vector3.zero), SignalArea.Within(0f)));
            Assert.AreEqual(1, first.Heard.Count);
            Assert.IsEmpty(second.Heard, "Unsubscribed before its turn: not reached");
            Assert.AreEqual(0, bus.Count);
        }

        [Test]
        public void Agent_ListensOnItsBus_AndRemembersTheSignal()
        {
            GraphRig rig = Rig();
            var bus = new SignalBus();
            rig.Agent.SetSignals(bus);

            ((ISignalListener)rig.Agent).Hear(new Signal("Alarm", Vector3.zero));
            rig.Step(DT);

            Assert.IsTrue(rig.Context.HasSignalSince("Alarm", 1f));
            rig.StepSeconds(1.2f);
            Assert.IsFalse(rig.Context.HasSignalSince("Alarm", 1f), "Remembered for the asked seconds only");
        }

        // ---------- cost and spread ----------

        [Test]
        public void Looks_TwentyAgentsAtOnce_AreSpreadOverTheInterval()
        {
            var profile = PerceptionProfile.Create(p => { p.VisionIntervalMin = 0.1f; p.VisionIntervalMax = 0.2f; });
            var firstLook = new List<float>();
            for (int i = 0; i < 20; i++)
            {
                var rig = new GraphRig(100 + i, trace: false);
                _rigs.Add(rig);
                rig.GivePerception(profile.Clone());
                firstLook.Add(SecondsUntil(rig, () => rig.Context.Perception.Looks > 0, 1f));
            }

            Assert.GreaterOrEqual(firstLook.Max() - firstLook.Min(), profile.VisionIntervalMax * 0.5f,
                "Not everybody looks on the same step");
        }

        [Test]
        public void OneLook_OneLineOfSightPerCandidate()
        {
            GraphRig rig = Rig();
            rig.Context.Sight = new FakeSight();
            rig.CreateTargetAt(new Vector3(5f, 0f, 0f));
            rig.Step(DT);
            int queries = rig.Context.SightQueries;
            int looks = rig.Context.Perception.Looks;

            rig.Step(DT, 10);

            Assert.AreEqual(rig.Context.Perception.Looks - looks, rig.Context.SightQueries - queries,
                "One ray per target per look");
        }

        [Test]
        public void Perception_StepsWithoutAllocating()
        {
            GraphRig rig = Rig();
            rig.Context.Sight = new FakeSight();
            rig.CreateTargetAt(new Vector3(5f, 0f, 0f));
            rig.Step(DT, 20);                                          // warm up: tracks and lists exist

            // Unity's own count (GC.GetAllocatedBytesForCurrentThread reads 0 in this editor whatever is allocated)
            Assert.That(() =>
            {
                for (int i = 0; i < 200; i++)
                    rig.Context.Perception.Step(DT);
            }, NoAllocation.Constraint, "No allocation in the perception's step");
        }

        [Test]
        public void Core_HasNoPhysics()
        {
            string[] references = typeof(AICharacter).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.IsFalse(references.Any(name => name.Contains("Physics")),
                "The core asks the physics only through probes: " + string.Join(", ", references));
        }
    }
}
