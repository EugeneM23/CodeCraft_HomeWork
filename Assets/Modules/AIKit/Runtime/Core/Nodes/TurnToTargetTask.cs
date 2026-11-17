using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Turns the character to its target in place, as a step turn: the body stops, the animator shows the turn to that
    /// side (Left / Right; Left 180 / Right 180 for a target behind — actions by name, handbook §20.1: the node asks,
    /// the body shows), and the body comes round over Turn Time, the target followed if it moves — by the clip itself on a
    /// body whose actions are moved by their clips (IClipAimActuator), the clip's turn aimed at the target. A target within Min
    /// Angle of the nose is simply faced, no turn shown. A turn that has started is not cut short (a Brain does not take
    /// the character elsewhere meanwhile). Success ("Done") — facing the target; Failure ("No Target") — there is none.
    /// For Notice: turn to what was seen, then draw the sword.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Movement/Turn To Target", "Task", "aikit.task.turn-to-target", Success = "Done", Order = 80,
        Failure = "No Target", Simple = true)]
    public sealed class TurnToTargetTask : AgentTask
    {
        private sealed class Mem
        {
            public bool Turning;
            public float Time;
            public Vector3 From;
            public bool ByClip;
        }

        /// <summary>Degrees the step turn clips turn by themselves — as their names say (Turn Left: 90, Turn Left 180).</summary>
        public const float TURN = 90f;

        public const float BIG_TURN = 180f;

        /// <summary>The Transform variable to turn to; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>Seconds the turn takes (the step turn clips are played to last as long).</summary>
        [Min(0.05f)] public float TurnTime = 1f;

        /// <summary>Degrees: a target closer to the nose than this is faced with no turn shown.</summary>
        [Range(0f, 90f)] public float MinAngle = 30f;

        /// <summary>Degrees: a target farther round than this is turned to with the big turn.</summary>
        [Range(90f, 180f)] public float BigAngle = 135f;

        [Header("Actions of the animator")]
        [ActionName] public string Left = "Turn Left";
        [ActionName] public string Right = "Turn Right";
        [ActionName] public string Left180 = "Turn Left 180";
        [ActionName] public string Right180 = "Turn Right 180";

        [NonSerialized] private BlackboardVariable<Transform> _target;

        /// <summary>A turn that has started runs to its end.</summary>
        public override bool CommitsFor(AgentContext context) => Memory<Mem>(context).Turning;

        private Transform TargetOf(AgentContext context) =>
            string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);

        private static Vector3 Flat(Vector3 direction) => new(direction.x, 0f, direction.z);

        // Where the body looks: its own word (Body.Forward — a Simple Body turns no transform, its senses look there), else
        // its object's forward
        private static Vector3 Looks(AgentContext context)
        {
            Vector3 looks = context.Actuator != null ? Flat(context.Actuator.Body.Forward) : Vector3.zero;
            if (looks.sqrMagnitude < 1e-6f)
                looks = Flat(context.Self != null ? context.Self.forward : Vector3.forward);
            return looks.sqrMagnitude > 1e-6f ? looks.normalized : Vector3.forward;
        }

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Turning = false;
            m.ByClip = false;
            m.Time = 0f;
            if (context == null)
                return;

            context.Navigation?.Clear();
            context.Actuator?.Stop();
            Transform target = TargetOf(context);
            if (target == null)
                return;

            m.From = Looks(context);
            float angle = Vector3.SignedAngle(m.From, Flat(target.position - context.Position), Vector3.up);
            if (Mathf.Abs(angle) < MinAngle)
                return;

            m.Turning = true;
            bool big = Mathf.Abs(angle) > BigAngle;
            string turn = angle > 0f ? (big ? Right180 : Right) : (big ? Left180 : Left);
            context.Actions.Begin(turn);

            // A body whose actions are moved by their clips is turned by the clip, aimed at the target: the feet and the
            // turn go together (handbook §20.2)
            m.ByClip = context.Actuator is IClipAimActuator clip && clip.ActionsByClip &&
                       context.Actions.CanShow(turn);
            if (m.ByClip)
                ((IClipAimActuator)context.Actuator).TurnByClipTo(target, big ? BIG_TURN : TURN);
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            Transform target = TargetOf(context);
            if (target == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            Vector3 to = Flat(target.position - context.Position);
            float done = 1f;
            if (m.Turning)
            {
                m.Time += dt;
                done = Mathf.Clamp01(m.Time / TurnTime);
            }

            // Round from where it looked to where the target is now, as far as the time has come. Turned by the clip,
            // the body is only held (nobody else turns it) until the clip is through; then it faces the target, for
            // whatever the target moved at the very end
            bool face = Claim(context, BodyAxis.Face, MoveAuthority.Frozen);
            if (face && !(m.ByClip && done < 1f))
            {
                float angle = Vector3.SignedAngle(m.Turning && !m.ByClip ? m.From : to, to, Vector3.up);
                Vector3 facing = Quaternion.AngleAxis(angle * done, Vector3.up) * (m.Turning && !m.ByClip ? m.From : to);
                context.Actuator?.Face(facing);
            }

            if (done < 1f)
                return TaskStatus.Running;

            m.ByClip = false;                           // the clip plays out aimed; the body lets go of the aim once it is over
            m.Turning = false;
            return TaskStatus.Success;
        }

        private static void EndClipTurn(AgentContext context, Mem m)
        {
            if (m.ByClip && context?.Actuator is IClipAimActuator clip)
                clip.EndClipTurn();
            m.ByClip = false;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            Mem m = Memory<Mem>(context);
            if (m.Turning)
                context?.Actions.End(true);             // taken away midway: the turn shown does not play on
            EndClipTurn(context, m);
            m.Turning = false;
        }
    }
}
