using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Comes up to an object (a Transform variable) and keeps StopDistance from it: the common work of Chase (straight
    /// to it) and Flank (around to the agent's own side). Stays near the target for as long as there is one — or,
    /// with FinishWhenCaught, ends the moment it is close enough. Failure — no target, or no way to it.
    /// </summary>
    [Serializable]
    public abstract class ApproachTask : AgentTask
    {
        /// <summary>The Transform variable (of the agent) to follow.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>
        /// How far from the target to stand (over the ground): within the reach of the attack that follows. 0 — well
        /// inside the reach of the weapon in the hand (0.8 × it, step 90: the distances are the weapon's).
        /// </summary>
        public BlackboardValue<float> StopDistance = 1.5f;

        /// <summary>Stop Distance with no weapon in the hand (the old default).</summary>
        public const float NO_WEAPON_STOP = 1.5f;

        /// <summary>On — the task ends when close enough; off — it stays close for as long as there is a target.</summary>
        public bool FinishWhenCaught;

        /// <summary>Stop Distance as it works for this agent now (0 — from the weapon in its hand).</summary>
        public float StopFor(AgentContext context) =>
            WeaponDistances.Resolve(StopDistance.Get(context), context, WeaponDistances.STOP, NO_WEAPON_STOP);

        // Everyone who comes for the target takes a place on its ring (the first attacker's is where it already is) —
        // what the block does by itself is said on its node (step 90)
        public override string Requirement => WeaponDistances.Join("takes a place on the ring",
            WeaponDistances.Say("stops at", StopDistance, WeaponDistances.STOP, NO_WEAPON_STOP));

        /// <summary>How near to its place on the circle counts as standing there (an agent that goes around).</summary>
        private const float ARRIVE_DISTANCE = 0.5f;

        /// <summary>Degrees of the circle still to go at which the agent stops circling and goes to its place.</summary>
        private const float CLOSE_IN_ANGLE = 30f;

        /// <summary>How far ahead along the circle the agent aims, degrees: the way is an arc, not a chord.</summary>
        private const float ARC_STEP = 40f;

        /// <summary>
        /// Metres the target must go past Stop Distance before a body that has come up walks again. Without it a body
        /// standing at the very edge takes a path and drops it every other step — it shivers on the spot
        /// (handbook §14.1: hysteresis on the boundary, or the picture dithers).
        /// </summary>
        private const float KEEP_SLACK = 0.35f;

        private sealed class Mem
        {
            public bool Caught;
        }

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        /// <summary>The agent comes up from its own side of the target, not straight (Flank).</summary>
        protected abstract bool GoesAround { get; }

        /// <summary>How wide the way around is (an agent that goes around); not above StopDistance — straight to its place.</summary>
        protected virtual float AroundRadius(AgentContext context) => 0f;

        protected override void OnEnter(AgentContext context) => Memory<Mem>(context).Caught = false;

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            INavigator navigation = context?.Navigation;
            if (navigation == null)
                return TaskStatus.Failure;

            Mem memory = Memory<Mem>(context);
            float slack = memory.Caught ? KEEP_SLACK : 0f;       // come up already: it takes a step away to set it off

            Transform target = (string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target));
            if (target == null)
                return TaskStatus.Failure;              // lost: nobody to follow

            // Everyone who comes for the target takes a place on its ring; the first attacker's place is the side it is
            // already on, and the rest are shared out evenly from there (AttackSlots.PlaceAround)
            bool first = AttackSlots.Leader(context, target) == null;
            AttackSlots.PlaceAround(context, target, out float mine, first);

            if (GoesAround && !first && TrySide(context, target, mine, out Vector3 place))
            {
                if (Vector3.Distance(context.Position, place) <= ARRIVE_DISTANCE + slack)
                    return Caught(navigation, memory);

                // The way around: along the circle first; a waypoint with no way to it (off the ground) — to the place
                if (Go(context, navigation, WaypointTo(context, target, place), dt) || Go(context, navigation, place, dt))
                    return TaskStatus.Running;
            }

            // Straight to it: no going around, nobody to take a side from (the first attacker, or alone), or no way there
            Vector3 offset = target.position - context.Position;
            offset.y = 0f;
            if (offset.magnitude <= StopFor(context) + slack)
                return Caught(navigation, memory);

            memory.Caught = false;
            navigation.SetDestination(target.position);
            if (!navigation.Status.IsMoving())
                return TaskStatus.Failure;              // no way to it

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return TaskStatus.Running;
        }

        private TaskStatus Caught(INavigator navigation, Mem memory)
        {
            memory.Caught = true;
            navigation.Clear();
            return FinishWhenCaught ? TaskStatus.Success : TaskStatus.Running;
        }

        // One step towards the point; false — there is no way to it
        private bool Go(AgentContext context, INavigator navigation, Vector3 point, float dt)
        {
            if (!navigation.SetDestination(point).CanGo() || !navigation.Status.IsMoving())
                return false;

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
            return true;
        }

        // Where this agent's own place on the ring is, at Stop Distance from the target
        private bool TrySide(AgentContext context, Transform target, float angle, out Vector3 place)
        {
            place = target.position + AttackSlots.DirectionOf(angle) * StopFor(context);
            place.y = context.Position.y;
            return true;
        }

        // Where to go right now to end up at the place: the place itself, or — while a wide way around is asked and a
        // good part of the circle is still ahead — a point further along the circle of the radius around the target
        private Vector3 WaypointTo(AgentContext context, Transform target, Vector3 place)
        {
            float radius = AroundRadius(context);
            if (radius <= StopFor(context))
                return place;

            Vector3 center = target.position;
            Vector3 fromCenter = context.Position - center;
            Vector3 toPlace = place - center;
            fromCenter.y = toPlace.y = 0f;
            if (fromCenter.sqrMagnitude < 1e-4f || toPlace.sqrMagnitude < 1e-4f)
                return place;

            float left = Vector3.SignedAngle(fromCenter, toPlace, Vector3.up);
            if (Mathf.Abs(left) <= CLOSE_IN_ANGLE)
                return place;

            float step = Mathf.Sign(left) * Mathf.Min(Mathf.Abs(left), ARC_STEP);
            Vector3 along = Quaternion.AngleAxis(step, Vector3.up) * fromCenter.normalized;
            Vector3 waypoint = center + along * radius;
            waypoint.y = place.y;
            return waypoint;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            context?.Navigation?.Clear();
            if (reason != ExitReason.Finished)
                context?.Actuator?.Stop();
        }
    }

    /// <summary>
    /// Follows an object straight: runs to it and keeps StopDistance from it. Success ("Caught", with
    /// FinishWhenCaught) — close enough; Failure ("Lost") — no target, or no way to it.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Chase", "Task", "aikit.task.chase", Success = "Caught", Failure = "Lost", Simple = true)]
    public sealed class ChaseTask : ApproachTask
    {
        protected override bool GoesAround => false;
    }

    /// <summary>
    /// Comes up to an object from the agent's own side: the one who attacks first goes straight, and this agent takes
    /// its share of the circle around the target — two stand opposite, three at 120° — so a group surrounds the
    /// target instead of crowding behind it. Who is first is read from the attack places (Take Attack Slot). With
    /// nobody to take a side from the way is straight, as Chase. Success ("In Place") — it stands at its place;
    /// Failure ("Lost") — no target, or no way to it.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Flank", "Task", "aikit.task.flank", Success = "In Place", Failure = "Lost", Simple = true)]
    public sealed class FlankTask : ApproachTask
    {
        public FlankTask() => FinishWhenCaught = true;

        /// <summary>
        /// How wide the way around is: the agent keeps this far from the target while it circles to its side, and
        /// only then closes in to StopDistance. Not above StopDistance — it goes straight to its place. 0 — just outside
        /// the reach of the weapon in the hand (1.1 × it).
        /// </summary>
        public BlackboardValue<float> BypassRadius = 3f;

        /// <summary>Bypass Radius with no weapon in the hand (the old default).</summary>
        public const float NO_WEAPON_BYPASS = 3f;

        protected override bool GoesAround => true;

        protected override float AroundRadius(AgentContext context) =>
            WeaponDistances.Resolve(BypassRadius.Get(context), context, WeaponDistances.AROUND, NO_WEAPON_BYPASS);

        public override string Requirement => WeaponDistances.Join(
            WeaponDistances.Join(base.Requirement, "goes straight when it attacks first"),
            WeaponDistances.Say("goes round at", BypassRadius, WeaponDistances.AROUND, NO_WEAPON_BYPASS));
    }
}
