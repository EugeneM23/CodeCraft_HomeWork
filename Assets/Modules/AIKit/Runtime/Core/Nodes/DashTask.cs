using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A dash — a dodge, a hop back — relative to the target the character faces (handbook §20b.4 p. 6: between the
    /// series; §19.2: the same move a reaction would make). The body shows the dash's action and goes exactly as far
    /// as the dodge clip carries it, over the clip's time (handbook §20.3: the numbers are the clip's — the weapon's
    /// dash of that direction, measured by the setup), Distance × scales it. A dash that has started is not cut short:
    /// the Brain does not take the character elsewhere meanwhile. Done — the dash is over; Blocked — no target, no dash
    /// that way on the weapon, a body that cannot dash, or something in the way (Aside tries the other side first).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Dash", "Task", "aikit.task.dash", Success = "Done", Failure = "Blocked", Simple = true, Order = 120)]
    public sealed class DashTask : AgentTask
    {
        private sealed class Mem
        {
            public bool Started;
            public float Left;
        }

        /// <summary>The Transform variable to dash relative to; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        public DashDirection Direction = DashDirection.Back;

        /// <summary>How far, as a part of the clip's own distance (1 — exactly the clip's: no sliding).</summary>
        [Min(0f)] public float DistanceScale = 1f;

        [NonSerialized] private BlackboardVariable<Transform> _target;

        /// <summary>A dash that has started runs to its end.</summary>
        public override bool CommitsFor(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            return m.Started && m.Left > 0f;
        }

        private Transform TargetOf(AgentContext context) =>
            string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Started = false;
            m.Left = 0f;
            context?.Navigation?.Clear();
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            Transform target = TargetOf(context);
            Vector3 inward = Vector3.zero;
            if (target != null)
            {
                inward = target.position - context.Position;
                inward.y = 0f;
                inward = inward.sqrMagnitude > 1e-6f ? inward.normalized : Vector3.zero;
            }

            if (!m.Started)
                return Start(context, m, target, inward) ? TaskStatus.Running : TaskStatus.Failure;

            // The body is the dash's: nobody else moves it meanwhile; the eyes stay on the target
            Claim(context, BodyAxis.Move, MoveAuthority.Traversal);
            if (inward != Vector3.zero && Claim(context, BodyAxis.Face, MoveAuthority.Traversal))
                context.Actuator?.Face(inward);

            m.Left -= dt;
            return m.Left > 1e-5f ? TaskStatus.Running : TaskStatus.Success;
        }

        private bool Start(AgentContext context, Mem m, Transform target, Vector3 inward)
        {
            if (target == null || inward == Vector3.zero || context.Actuator is not IDashActuator body)
                return false;

            DashDirection direction = Direction;
            bool aside = direction == DashDirection.Aside;
            if (aside)
                direction = context.Chance(0.5f) ? DashDirection.Left : DashDirection.Right;

            if (!TryDash(context, body, direction, inward, m) &&
                !(aside && TryDash(context, body, direction == DashDirection.Left ? DashDirection.Right : DashDirection.Left, inward, m)))
                return false;

            m.Started = true;
            return true;
        }

        private bool TryDash(AgentContext context, IDashActuator body, DashDirection direction, Vector3 inward, Mem m)
        {
            WeaponDash dash = context.Weapon != null ? context.Weapon.DashTo(direction) : null;
            if (dash == null)
                return false;

            Vector3 displacement = Way(direction, inward) * (dash.Distance * Mathf.Max(0f, DistanceScale));
            if (!body.CanDash(displacement))
                return false;

            if (!string.IsNullOrEmpty(dash.Action))
                context.Actions.Begin(dash.Action);
            body.Dash(displacement, dash.Time);
            m.Left = dash.Time;
            return true;
        }

        /// <summary>The direction of a dash in the world: forward is toward the target, left is to the left of that.</summary>
        public static Vector3 Way(DashDirection direction, Vector3 inward)
        {
            Vector3 left = new Vector3(-inward.z, 0f, inward.x);
            Vector3 way = direction switch
            {
                DashDirection.Back => -inward,
                DashDirection.Forward => inward,
                DashDirection.Left => left,
                DashDirection.Right => -left,
                DashDirection.BackLeft => -inward + left,
                DashDirection.BackRight => -inward - left,
                DashDirection.ForwardLeft => inward + left,
                DashDirection.ForwardRight => inward - left,
                _ => left,
            };
            return way.normalized;
        }
    }
}
