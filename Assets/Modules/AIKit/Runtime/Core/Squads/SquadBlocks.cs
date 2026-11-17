using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A block of a squad's command graph: what the formation does while its decision is in charge — it leads the
    /// formation (moves its anchor, turns it, lays it out as ranks or rings), never a body (each member keeps its slot by
    /// its own Keep Formation, or does something more important of its own). Runs every step while its node is on; it
    /// ends only when it says it is done (Form Ring with Stand For). Failure ("No Squad") — it runs outside a squad's
    /// command graph (in a character's graph).
    /// </summary>
    [Serializable]
    public abstract class SquadBlockTask : AgentTask
    {
        // Shared by every squad of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        /// <summary>The object of the variable; empty — the Squad Brain's target (what the members see).</summary>
        protected Transform TargetOf(AgentContext context, string variable) =>
            string.IsNullOrEmpty(variable) ? context.BrainTarget : _target.Get(context.Blackboard, variable);

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            Squad squad = context?.Leads;
            if (squad == null)
                return TaskStatus.Failure;

            squad.Led();
            Lead(context, squad, dt);
            return IsDone(context, squad, dt) ? TaskStatus.Success : TaskStatus.Running;
        }

        /// <summary>One step of leading the formation.</summary>
        protected abstract void Lead(AgentContext context, Squad squad, float dt);

        /// <summary>The block has done its work after this step (most never are: they lead until taken away).</summary>
        protected virtual bool IsDone(AgentContext context, Squad squad, float dt) => false;

        protected static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }

    /// <summary>
    /// March To Target: the formation in ranks turns to face the target (it wheels round its middle; a turn wider than
    /// 120° is an about-face) and marches on it at Speed — slower while the members lag (handbook §20a.2: the speed of the
    /// slowest) — until its front rank is Stop At in front of the target. The pursuit of a target that ran is this block
    /// too: the ranks march after it. A line of archers marches to Stop At = its shooting distance.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Squad/March To Target", "Task", "aikit.task.march-to-target", Failure = "No Squad", Simple = true,
        Order = 20, Endless = true)]
    public sealed class MarchToTargetTask : SquadBlockTask
    {
        /// <summary>The Transform variable to march on; empty — the Squad Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>How fast the formation marches, metres per second.</summary>
        [Min(0f)] public float Speed = 2.2f;

        /// <summary>How far in front of the target the front rank stops, metres.</summary>
        [Min(0f)] public float StopAt = 2.5f;

        /// <summary>Degrees per second the ranks wheel to face the target.</summary>
        private const float WHEEL = 60f;

        protected override void Lead(AgentContext context, Squad squad, float dt)
        {
            Transform target = TargetOf(context, Target);
            squad.FormPhalanx(target);
            if (target == null)
                return;

            squad.Turn(target.position - squad.Centre, WHEEL, dt);
            if (squad.AheadOfFront(target.position) <= StopAt)
                return;                                 // the front is there: it holds

            Vector3 goal = target.position - squad.Facing * StopAt;
            goal.y = squad.Anchor.y;
            squad.MoveAnchorTo(goal, Speed * squad.PaceFactor, dt);
        }
    }

    /// <summary>
    /// Form Ring: the formation becomes rings round the target — the front rank (the members nearest to it) the inner
    /// ring of Radius (wider when a rank does not fit round it), each rank behind it a rank's gap farther out, every member
    /// on its own place in the order they stand round the target, so nobody runs across the ring. The rings drift after
    /// the target slowly (Follow Speed) to keep it in the middle. Middle — a member who stands in the middle instead,
    /// facing the way the squad's banner faces (the mayor at a meeting); empty — nobody. Stand For — once the squad has
    /// gathered (the one in the middle on its place and nine in ten on theirs, or GATHER_LIMIT seconds after the rings
    /// were formed), the rings stand this
    /// long and the block is done ("Done"); 0 — it leads until its decision lets go.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Squad/Form Ring", "Task", "aikit.task.form-ring", Success = "Done", Failure = "No Squad", Order = 30,
        Simple = true, Endless = true)]
    public sealed class FormRingTask : SquadBlockTask
    {
        /// <summary>The part of the squad on its places at which it counts as gathered.</summary>
        public const float GATHERED = 0.9f;

        /// <summary>Seconds after the rings were formed when the squad counts as gathered whoever is still missing.</summary>
        public const float GATHER_LIMIT = 30f;

        /// <summary>The Transform variable to stand round; empty — the Squad Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>The Transform variable of the member who stands in the middle; empty — nobody.</summary>
        [VariableName(VariableType.Transform)] public string Middle;

        /// <summary>The radius of the inner ring, metres.</summary>
        [Min(0.5f)] public float Radius = 2.6f;

        /// <summary>How fast the rings drift after the target, metres per second.</summary>
        [Min(0f)] public float FollowSpeed = 1.2f;

        /// <summary>Seconds the rings stand once the squad has gathered, then Done; 0 — until the decision lets go.</summary>
        [Min(0f)] public float StandFor;

        private sealed class Mem
        {
            public float Formed;
            public float Stood;
        }

        // Shared by every squad of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _middle;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Formed = 0f;
            m.Stood = 0f;
        }

        protected override void Lead(AgentContext context, Squad squad, float dt)
        {
            Transform target = TargetOf(context, Target);
            if (target == null)
                return;

            Transform middle = string.IsNullOrEmpty(Middle) ? null : _middle.Get(context.Blackboard, Middle);
            squad.FormRing(target.position, Radius, middle);
            squad.MoveAnchorTo(target.position, FollowSpeed, dt);
        }

        /// <summary>
        /// The squad counts as gathered now: the one in the middle stands there and nine in ten are on their places — or
        /// GATHER_LIMIT seconds have passed since the rings were formed, whoever is still missing.
        /// </summary>
        public bool IsGathered(AgentContext context) =>
            context?.Leads != null && (context.Leads.Cohesion >= GATHERED && context.Leads.MiddleInPlace ||
                                       Memory<Mem>(context).Formed >= GATHER_LIMIT);

        /// <summary>Seconds the rings have stood since the squad gathered.</summary>
        public float StoodFor(AgentContext context) => Memory<Mem>(context).Stood;

        protected override bool IsDone(AgentContext context, Squad squad, float dt)
        {
            Mem m = Memory<Mem>(context);
            m.Formed += dt;
            if (StandFor <= 0f || TargetOf(context, Target) == null)
                return false;

            if (m.Stood > 0f || IsGathered(context))
                m.Stood += dt;
            return m.Stood >= StandFor;
        }
    }

    /// <summary>
    /// Hold Line: the formation stands in ranks facing the foes — it creeps on (slowly) only while the nearest foe in front
    /// of it is farther than Close To from its front, never back, and turns slowly to face the middle of the foes within
    /// Foes Within of the target only when they are more than 20° off (foes roughly in front are met by the line as it
    /// stands; it does not swing its wings round a group moving in front of it).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Squad/Hold Line", "Task", "aikit.task.hold-line", Failure = "No Squad", Simple = true, Order = 40,
        Endless = true)]
    public sealed class HoldLineTask : SquadBlockTask
    {
        /// <summary>The Transform variable of the foes' line; empty — the Squad Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>The front creeps on until the nearest foe in front of it is this far, metres.</summary>
        [Min(0f)] public float CloseTo = 2.2f;

        /// <summary>The foes this near the target are the line it faces, metres.</summary>
        [Min(0f)] public float FoesWithin = 8f;

        /// <summary>Metres per second the line creeps on, degrees per second it turns, and the turn it lets be.</summary>
        private const float CREEP = 0.8f;
        private const float TURN = 20f;
        private const float TURN_DEADBAND = 20f;

        protected override void Lead(AgentContext context, Squad squad, float dt)
        {
            Transform target = TargetOf(context, Target);
            squad.FormPhalanx(target);
            if (target == null)
                return;

            Vector3 toFoes = Flat(squad.FoesMiddle(target, FoesWithin) - squad.Centre);
            if (Vector3.Angle(squad.Facing, toFoes) > TURN_DEADBAND)
                squad.Turn(toFoes, TURN, dt);

            Transform ahead = squad.NearestAhead(FoesWithin + CloseTo);
            if (ahead == null)
                return;

            float gap = squad.AheadOfFront(ahead.position);
            if (gap > CloseTo + 0.3f)
                squad.MoveAnchorTo(squad.Anchor + squad.Facing * (gap - CloseTo), CREEP * squad.PaceFactor, dt);
        }
    }

    /// <summary>
    /// Return To Banner: the formation in ranks walks back to its banner (the AI Squad's object) at Speed and turns to
    /// face the way the banner faces — where the squad stands when there is nothing to do.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Squad/Return To Banner", "Task", "aikit.task.return-to-banner", Failure = "No Squad", Simple = true,
        Order = 50, Endless = true)]
    public sealed class ReturnToBannerTask : SquadBlockTask
    {
        /// <summary>How fast the formation walks back, metres per second.</summary>
        [Min(0f)] public float Speed = 1.5f;

        /// <summary>Degrees per second the ranks turn to face the banner's way.</summary>
        private const float TURN = 45f;

        protected override void Lead(AgentContext context, Squad squad, float dt)
        {
            squad.FormPhalanx();
            Transform banner = squad.Banner;
            if (banner == null)
                return;

            squad.Turn(banner.forward, TURN, dt);
            squad.MoveAnchorTo(banner.position, Speed * squad.PaceFactor, dt);
        }
    }
}
