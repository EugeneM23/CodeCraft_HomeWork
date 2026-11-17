using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A charge: a strong move in three steps, the rhythm of handbook §19.1 — warning, window, punishment. First the body
    /// stands and gathers its strength for Charge Time (the action Charge Up: the warning), turning to the target no
    /// faster than Aim Speed — not at once, so a target that runs aside is followed late. Then it shoots off along the
    /// line it aimed — Distance metres at Speed, stopping short of a wall (the body's walk probe) — and does not turn on
    /// the way: a target that stepped aside is passed. Last it stands for Recovery seconds (the window: whoever dodged
    /// strikes its back). The whole charge runs to its end (the Brain does not take the agent away meanwhile).
    /// The rush goes by the body's dash when it has one (exactly this far in exactly this time), else by the stick.
    /// The charge is a move (step 90, C14): its numbers and actions are the weapon's in the hand (its Rush, like its
    /// dashes); the node's own numbers are used only with a weapon that has none (graphs of before — the node says so).
    /// It deals no damage of its own, as before.
    /// Success ("Done"). Failure ("Can't Charge") — no target when it should start.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Charge", "Task", "aikit.task.charge", Success = "Done", Failure = "Can't Charge",
        Simple = true)]
    public sealed class ChargeTask : AgentTask
    {
        /// <summary>What the charge is at for one agent.</summary>
        public enum Phase
        {
            Gathering,
            Rushing,
            Recovering
        }

        /// <summary>Metres shorter than a wall ahead the rush stops.</summary>
        private const float WALL_GAP = 0.3f;

        private sealed class Mem
        {
            public Phase Phase;
            public float Time;
            public float RushTime;
            public Vector3 Aim;
            public bool Dashing;
            public bool Acting;
            public WeaponRush Rush;         // the weapon's charge it was started with; null — the node's own numbers
        }

        /// <summary>The Transform variable (of the agent) to charge at; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>Seconds it gathers its strength before it shoots off (the warning) — with a weapon that has no Rush.</summary>
        public BlackboardValue<float> ChargeTime = 2f;

        /// <summary>Degrees per second it turns to the target while it gathers its strength — with no Rush.</summary>
        public BlackboardValue<float> AimSpeed = 90f;

        /// <summary>Metres it rushes — with no Rush.</summary>
        public BlackboardValue<float> Distance = 12f;

        /// <summary>Metres per second of the rush — with no Rush.</summary>
        public BlackboardValue<float> Speed = 14f;

        /// <summary>Seconds it stands after the rush (the window to strike its back) — with no Rush.</summary>
        public BlackboardValue<float> Recovery = 2f;

        /// <summary>The action the body shows while it gathers its strength; empty — none — with no Rush.</summary>
        [ActionName] public string ChargeAction = "Charge Up";

        /// <summary>The action the body shows while it rushes; empty — none — with no Rush.</summary>
        [ActionName] public string RushAction = "Charge";

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        public override bool IsOneShot => true;

        /// <summary>Whose numbers the charge goes by, on the node.</summary>
        public override string Requirement => "numbers: the weapon's Rush (these only with a weapon that has none)";

        /// <summary>The whole charge runs to its end: gathering, rush and the stand after it.</summary>
        public override bool CommitsFor(AgentContext context) => IsRunningFor(context);

        /// <summary>Where the charge of this agent is now (tests, the debugger).</summary>
        public Phase PhaseOf(AgentContext context) => Memory<Mem>(context).Phase;

        /// <summary>The line the agent aims (and rushes) along now, flat.</summary>
        public Vector3 AimOf(AgentContext context) => Memory<Mem>(context).Aim;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Phase = Phase.Gathering;
            m.Time = 0f;
            m.RushTime = 0f;
            m.Dashing = false;
            m.Acting = false;
            m.Rush = context?.Weapon?.RushOrNone;
            m.Aim = Forward(context);
            context?.Navigation?.Clear();
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context?.Actuator == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            m.Time += dt;
            switch (m.Phase)
            {
                case Phase.Gathering:
                    return Gather(context, m, dt);
                case Phase.Rushing:
                    return Rush(context, m);
                default:
                    if (Claim(context, BodyAxis.Move, MoveAuthority.Frozen))
                        context.Actuator.Move(Vector3.zero);
                    return m.Time < RecoveryOf(context, m) ? TaskStatus.Running : TaskStatus.Success;
            }
        }

        // The numbers of this charge: the weapon's Rush, else the node's own
        private float RecoveryOf(AgentContext context, Mem m) => m.Rush?.Recovery ?? Recovery.Get(context);

        // Standing, the aim turns to the target no faster than Aim Speed; then the rush starts along it
        private TaskStatus Gather(AgentContext context, Mem m, float dt)
        {
            Transform target = string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);
            if (target == null && m.Time <= dt)
                return TaskStatus.Failure;              // nothing to charge at: it does not start

            if (m.Time <= dt)
            {
                context.Actuator.Stop();
                Show(context, m, m.Rush != null ? m.Rush.ChargeAction : ChargeAction);
            }

            if (target != null)
            {
                Vector3 to = target.position - context.Position;
                to.y = 0f;
                if (to.sqrMagnitude > 1e-4f)
                {
                    float aimSpeed = m.Rush?.AimSpeed ?? AimSpeed.Get(context);
                    float turn = Mathf.Max(0f, aimSpeed) * dt * Mathf.Deg2Rad;
                    m.Aim = Vector3.RotateTowards(m.Aim, to.normalized, turn, 0f).normalized;
                }
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Frozen))
                context.Actuator.Move(Vector3.zero);
            if (Claim(context, BodyAxis.Face, MoveAuthority.Frozen))
                context.Actuator.Face(m.Aim);
            if (m.Time < (m.Rush?.ChargeTime ?? ChargeTime.Get(context)))
                return TaskStatus.Running;

            // Off it goes: as far as asked, short of a wall
            float distance = Mathf.Max(0f, m.Rush?.Distance ?? Distance.Get(context));
            if (context.Walk != null)
                distance = Mathf.Min(distance, Mathf.Max(0f, context.Walk.FreeDistance(context.Position, m.Aim, distance + WALL_GAP) - WALL_GAP));
            float speed = Mathf.Max(0.1f, m.Rush?.Speed ?? Speed.Get(context));
            m.Phase = Phase.Rushing;
            m.Time = 0f;
            m.RushTime = distance / speed;
            m.Dashing = false;
            Show(context, m, m.Rush != null ? m.Rush.RushAction : RushAction);
            if (distance > 0.05f && context.Actuator is IDashActuator dash)
            {
                dash.Dash(m.Aim * distance, m.RushTime);
                m.Dashing = true;
            }

            return TaskStatus.Running;
        }

        // Along the aimed line, never turning: by the dash, or by the stick for the time the distance takes
        private TaskStatus Rush(AgentContext context, Mem m)
        {
            if (Claim(context, BodyAxis.Face, MoveAuthority.Frozen))
                context.Actuator.Face(m.Aim);

            bool going = m.Dashing
                ? context.Actuator is IDashActuator { IsDashing: true } && m.Time < m.RushTime + 0.5f
                : m.Time < m.RushTime;
            if (going)
            {
                if (!m.Dashing && Claim(context, BodyAxis.Move, MoveAuthority.Frozen))
                    context.Actuator.Move(m.Aim, 1f);
                return TaskStatus.Running;
            }

            m.Phase = Phase.Recovering;
            m.Time = 0f;
            context.Actuator.Stop();
            if (m.Acting)
                context.Actions.End(true);              // the rush is over: it stands, out of breath
            m.Acting = false;
            return RecoveryOf(context, m) > 0f ? TaskStatus.Running : TaskStatus.Success;
        }

        private static void Show(AgentContext context, Mem m, string action)
        {
            if (string.IsNullOrEmpty(action))
                return;

            context.Actions.Begin(action);
            m.Acting = context.Actions.CanShow(action);
        }

        // The flat direction the body looks (its object's forward); world forward without one
        private static Vector3 Forward(AgentContext context)
        {
            Vector3 forward = context?.Self != null ? context.Self.forward : Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context == null)
                return;

            Mem m = Memory<Mem>(context);
            if (m.Acting)
                context.Actions.End(true);
            m.Acting = false;
            if (reason != ExitReason.Finished)
                context.Actuator?.Stop();
        }
    }
}
