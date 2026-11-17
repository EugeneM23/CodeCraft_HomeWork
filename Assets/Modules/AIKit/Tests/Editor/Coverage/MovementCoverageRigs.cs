using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AIKit.Tests
{
    /// <summary>
    /// What the movement recheck's tests share (step 90 recheck, lane T4): agents of one world round a target with places
    /// to attack it from, the target itself, the step kind a body was told, and fakes of the parts a body answers with.
    /// </summary>
    internal static class MovementCoverageRigs
    {
        public const string TARGET = "Target";

        /// <summary>
        /// An awake agent of the world, thinking every step; its body and its object stand together (the places measure
        /// the object). The target goes into its Target variable.
        /// </summary>
        public static GraphRig RigIn(IAIWorld world, Vector3 at, Transform target = null, int seed = 1234,
            INavigator navigator = null)
        {
            var rig = new GraphRig(seed, trace: false, world: world, navigator: navigator) { ThinkInterval = 0f };
            rig.Agent.gameObject.SetActive(true);
            rig.Actuator.Position = at;
            rig.Agent.transform.position = at;
            if (target != null)
                rig.Agent.Blackboard.Set(TARGET, target);
            return rig;
        }

        /// <summary>The object to fight; places — how many its Attack Slots give (negative — it has none).</summary>
        public static Transform NewTarget(List<Object> made, Vector3 at, int places = -1)
        {
            var go = new GameObject("Target") { hideFlags = HideFlags.HideAndDontSave };
            made.Add(go);
            go.transform.position = at;
            if (places >= 0)
                go.AddComponent<AttackSlotCapacity>().Slots = places;
            return go.transform;
        }

        /// <summary>One agent at that place takes the target's attack place (so the rest wait); the holder is returned.</summary>
        public static GraphRig GivePlaceTo(IAIWorld world, Transform target, Vector3 at, float dt)
        {
            GraphRig holder = RigIn(world, at, target);
            var take = new TakeAttackSlotTask { Target = TARGET };
            for (int step = 0; step < 200; step++)
            {
                TaskStatus answer = take.Tick(holder.Context, dt);
                if (answer == TaskStatus.Success)
                    return holder;

                world.BeginStep(dt);
            }

            throw new InvalidOperationException("Set-up: the holder never got the place");
        }

        public static bool Holds(IAIWorld world, Transform target, GraphRig rig) =>
            world.Roster.EngagementOf(target).Tokens.Holds(rig.Agent, AttackSlots.POOL);

        /// <summary>The body moves where it was told, its object with it (one step of the body).</summary>
        public static void Apply(GraphRig rig, float dt)
        {
            rig.Actuator.Apply(dt);
            rig.Agent.transform.position = rig.Actuator.Position;
        }

        /// <summary>
        /// Which way the body was told to go, relative to the target, as the waiting blocks name it: 0 — stands, 1 / 2 —
        /// left / right (the bearing round the target grows to the left), 3 / 4 — in / out.
        /// </summary>
        public static int StepKind(Vector3 velocity, Vector3 toTarget)
        {
            if (velocity.magnitude < 0.05f)
                return 0;
            Vector3 inward = Flat(toTarget).normalized;
            float along = Vector3.Dot(velocity.normalized, inward);
            var left = new Vector3(-inward.z, 0f, inward.x);
            return along > 0.7f ? 3 : along < -0.7f ? 4 : Vector3.Dot(velocity, left) > 0f ? 1 : 2;
        }

        /// <summary>
        /// A context with no character and no world (so no ring round any target): its body is a test body at this
        /// place, the Brain's target this one.
        /// </summary>
        public static AgentContext Bare(Vector3 at, Transform brainTarget, int seed = 1234)
        {
            var actuator = new TestActuator { Position = at };
            return new AgentContext(null, null, seed) { Actuator = actuator, BrainTarget = brainTarget };
        }

        public static TestActuator BodyOf(AgentContext context) => (TestActuator)context.Actuator;

        public static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }

    /// <summary>
    /// GC allocations made by a piece of code, counted by the profiler's GC.Alloc recorder (as the Test Framework's
    /// AllocatingGCMemory constraint counts them). GC.GetAllocatedBytesForCurrentThread is not used: under the editor's
    /// Mono it does not see allocations (a new array a call reads as 0 bytes). The first call of a piece of code that
    /// uses generic types allocates once in the runtime (its compilation): run it once before it is counted.
    /// </summary>
    internal static class Allocations
    {
        public static int Count(Action action)
        {
            UnityEngine.Profiling.Recorder recorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            try
            {
                action();
            }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
            }

            return recorder.sampleBlockCount;
        }
    }

    /// <summary>A line of sight the test opens and closes (a wall between the agent and the target).</summary>
    internal sealed class CoverageSight : ISightProbe
    {
        public bool Clear;

        public bool HasLineOfSight(Vector3 from, Vector3 to) => Clear;
    }

    /// <summary>
    /// A navigator the test scripts: it says Following for a while after each destination, then what the test says
    /// (Unreachable — it gave up on the way); a destination it is told to refuse is Unreachable at once.
    /// </summary>
    internal sealed class ScriptedWayNavigator : INavigator
    {
        private Vector3 _destination;
        private float _since;
        private bool _has;

        /// <summary>Seconds of Following after a destination; then GivesUpAs. Infinity — it follows for ever.</summary>
        public float FollowsFor = float.PositiveInfinity;

        public NavStatus GivesUpAs = NavStatus.Unreachable;

        /// <summary>Points it refuses at once (Unreachable); null — none.</summary>
        public Func<Vector3, bool> Refuses;

        public int Asked { get; private set; }

        public NavVerdict SetDestination(Vector3 point)
        {
            Asked++;
            if (Refuses != null && Refuses(point))
            {
                _has = false;
                return NavVerdict.Unreachable;
            }

            if (!_has || (point - _destination).sqrMagnitude > 1e-6f)
                _since = 0f;
            _destination = point;
            _has = true;
            return NavVerdict.Reachable;
        }

        public void Clear() => _has = false;

        public NavStatus Status => !_has ? NavStatus.Idle : _since < FollowsFor ? NavStatus.Following : GivesUpAs;

        public Vector3 Destination => _destination;

        public Vector3 SteeringTarget => _destination;

        public float RemainingDistance => _has ? 100f : float.PositiveInfinity;

        public NavEdgeKind CurrentEdge => NavEdgeKind.Walk;

        /// <summary>The time of the way goes on (the test calls it once per step).</summary>
        public void Advance(float dt) => _since += dt;

        public void Step(float dt)
        {
        }
    }
}
