using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Does something with an object of the world (step 90, C15 — "the action node"): runs the object's own scenario of
    /// the chosen interaction — the graph does not decide what happens, the object does (handbook §24.1): a rock or a pile
    /// gives a weapon to throw, a box is carried in the hands, a depot hands one out, a turret is manned (its gun taken up)
    /// or loaded with the box in the hands, a cover, a perch or an AI Object is occupied, facing the threat. The object
    /// comes by a wire into its Object pin — the variable a Find wrote; the character must stand at the object's place
    /// (Move To / Climb To before it). While the scenario runs the claim on the object is kept.
    /// Success ("Done"); Failure ("Failed") — no object wired in or in the variable, the character is not at it, the
    /// object does not offer that now (taken in the last moment, nothing to load it with), or it goes in the middle
    /// (destroyed, switched off: the action it asked the body to show is dropped at once).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Objects/Interact", "Task", "aikit.task.interact", Success = "Done", Failure = "Failed", Simple = true)]
    public sealed class InteractTask : AgentTask
    {
        private sealed class Mem
        {
            public readonly Interaction Interaction = new();
            public WorldObject Object;
            public bool Begun;
        }

        /// <summary>The Transform variable with the object (the one a Find wrote): only by a wire.</summary>
        [WireOnly] [VariableName(VariableType.Transform)] public string Object;

        /// <summary>What to do with it (what the object must offer).</summary>
        public Offer Interaction = Offer.Take;

        /// <summary>The Transform variable of the threat to face (a cover is taken against it); empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        // Shared by every agent of the graph: caches the names' ids only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _object;
        [NonSerialized] private BlackboardVariable<Transform> _target;

        public override string Requirement => string.IsNullOrEmpty(Target)
            ? "the object's own scenario; target: Brain's"
            : "the object's own scenario";

        /// <summary>The object of the agent's variable as an object of the world; null — none.</summary>
        public WorldObject ObjectOf(AgentContext context)
        {
            if (context == null || string.IsNullOrEmpty(Object))
                return null;

            Transform holder = _object.Get(context.Blackboard, Object);
            return holder != null ? holder.GetComponentInParent<WorldObject>() : null;
        }

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Object = null;
            m.Begun = false;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null)
                return TaskStatus.Failure;

            Mem m = Memory<Mem>(context);
            Interaction interaction = m.Interaction;
            if (!m.Begun)
            {
                WorldObject target = ObjectOf(context);
                if (target == null || !target.IsLive)
                    return TaskStatus.Failure;          // nothing wired in, or the variable holds no object

                Transform threat = ThreatOf(context);
                if (!target.IsAtPlace(Interaction, context, threat, WorldObject.LEAVE_SLACK))
                    return TaskStatus.Failure;          // not there: Move To first

                interaction.Reset(context, Interaction, threat);
                interaction.MayFace = Claim(context, BodyAxis.Face, MoveAuthority.Frozen);
                if (!target.Begin(interaction))
                {
                    target.End(interaction, ExitReason.Finished);
                    return TaskStatus.Failure;
                }

                m.Object = target;
                m.Begun = true;
            }

            if (m.Object == null || !m.Object.IsLive)
            {
                Drop(context, m);                       // the object went in the middle (destroyed, switched off)
                return TaskStatus.Failure;
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Frozen))
                context.Actuator?.Move(Vector3.zero);
            interaction.MayFace = Claim(context, BodyAxis.Face, MoveAuthority.Frozen);
            context.WorldObjects.Renew(m.Object, context.Agent);

            TaskStatus status = m.Object.Tick(interaction, dt);
            interaction.Time += dt;
            if (status == TaskStatus.Running)
                return status;

            m.Object.End(interaction, ExitReason.Finished);
            m.Begun = false;
            return status;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context == null)
                return;

            Mem m = Memory<Mem>(context);
            if (m.Begun && m.Object != null)
                m.Object.End(m.Interaction, reason);
            else if (m.Begun)
                Drop(context, m);
            m.Begun = false;
            m.Object = null;
        }

        // The scenario is torn away because its object went: the object ends it while it is still there (what was half
        // done is undone); a destroyed one cannot, so the action it asked the body to show is dropped here, at once
        private static void Drop(AgentContext context, Mem m)
        {
            if (m.Begun && m.Object != null)
            {
                m.Object.End(m.Interaction, ExitReason.Interrupted);
            }
            else if (m.Interaction.ShowsAction)
            {
                context.Actions.End(true);
                m.Interaction.ShowsAction = false;
            }

            m.Begun = false;
        }

        private Transform ThreatOf(AgentContext context) =>
            string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);
    }
}
