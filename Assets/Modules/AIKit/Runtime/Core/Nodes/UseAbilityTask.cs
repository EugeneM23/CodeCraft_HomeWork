using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Uses an ability once: the one named on the node (an ability asset), or — empty — the one the decision in charge
    /// chose (Use Ability's own, the pick of Use Any Ability: the canvas marks it "ability: decision's", as "target:
    /// Brain's"). The node chooses nothing itself (step 90: the choice is the decision's — handbook §17). The character
    /// prepared its own runner for every ability its graph names.
    /// Target names the object the ability is used on — a Transform variable; empty — the Brain's target.
    /// An ability that takes turns (its queue's pool) asks the target's places first, and has three outcomes instead of
    /// two: granted — it plays; filed and waiting — the task runs on (a moment only: a canvas node does not stand in a
    /// queue); refused — Failure. While the ability plays the body is frozen for every other writer (MoveAuthority.Frozen
    /// on both axes) and turns to the target until the release. Running while it plays, Success when it is over, Failure
    /// when it could not be used. Interrupted from the outside the ability is cancelled; the place goes back either way.
    /// On the canvas the node has two pins: "Used" — the ability was used and is over (its pause starts from here, handbook
    /// §16), "Not Used" — it could not be used now (no place in its pool, out of range, resting): nothing to count.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Combat/Use Ability", "Task", "aikit.task.use-ability", Success = "Used", Failure = "Not Used", Simple = true)]
    public sealed class UseAbilityTask : AgentTask, IUsesAbilities
    {
        private sealed class Mem
        {
            public string Used;
            public string WaitingFor;
            public string Pool;
            public float Waited;
        }

        /// <summary>Seconds the node waits for a place in the ability's pool (the group answers once per step).</summary>
        private const float PLACE_WAIT = 0.25f;

        public UseAbilityTask()
        {
        }

        public UseAbilityTask(AbilityAsset ability) => Ability = ability;

        /// <summary>The Transform variable (of the agent) with the object to use the ability on; empty — the Brain's target.</summary>
        [VariableName(VariableType.Transform)] public string Target;

        /// <summary>The ability (a project asset, kept in the graph asset's table of objects); null — the decision's.</summary>
        [NonSerialized] public AbilityAsset Ability;

        /// <summary>Off — the task succeeds the moment the ability started.</summary>
        [HideInInspector] public bool WaitForFinish = true;

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _target;

        public string UsedBy(AgentContext context) => Memory<Mem>(context).Used;

        /// <summary>One action: the ability is used once and runs out by itself — no On Tick.</summary>
        public override bool IsOneShot => true;

        /// <summary>An ability that has started runs to its end: a reasoner does not switch away meanwhile.</summary>
        public override bool CommitsFor(AgentContext context) => Memory<Mem>(context).Used != null;

        /// <summary>The ability whose place request is filed and waiting (null — none).</summary>
        public string WaitingForBy(AgentContext context) => Memory<Mem>(context).WaitingFor;

        /// <summary>What the node does by itself, on the node: with no ability of its own it uses the decision's.</summary>
        public override string Requirement => Ability == null ? "ability: decision's" : null;

        public void CollectAbilities(List<AbilityAsset> abilities)
        {
            if (Ability != null)
                abilities.Add(Ability);
        }

        /// <summary>The ability this agent's run uses: its own, else the decision's; null — none.</summary>
        public AbilityAsset AbilityFor(AgentContext context) => Ability != null ? Ability : context?.ChosenAbility;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.Used = null;
            m.WaitingFor = null;
            m.Pool = null;
            m.Waited = 0f;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context?.Abilities == null)
                return TaskStatus.Failure;

            // Everything the ability asks about the target is asked about this node's object
            Transform aim = string.IsNullOrEmpty(Target) ? context.BrainTarget : _target.Get(context.Blackboard, Target);
            using (context.AimAt(aim))
                return Use(context, aim != null, dt);
        }

        // faces — the node was given its object: it turns to it by itself until the release (a canvas graph has no task
        // beside this one to do the turning)
        private TaskStatus Use(AgentContext context, bool faces, float dt)
        {
            Mem m = Memory<Mem>(context);
            AgentAbilities abilities = context.Abilities;
            if (m.Used == null)
            {
                AbilityAsset ability = AbilityFor(context);
                string id = m.WaitingFor ?? ability?.Name;
                if (string.IsNullOrEmpty(id))
                    return TaskStatus.Failure;

                switch (AskToken(context, abilities.DefinitionOf(id), faces))
                {
                    case TokenResult.Pending:
                        // A canvas node does not stand in a queue: no place within a moment — not now
                        m.Waited += dt;
                        if (faces && m.Waited >= PLACE_WAIT)
                        {
                            m.WaitingFor = null;
                            ReleaseToken(context);
                            return TaskStatus.Failure;
                        }

                        m.WaitingFor = id;               // filed: keep asking for the same one
                        context.Actuator?.Move(Vector3.zero);
                        return TaskStatus.Running;

                    case TokenResult.Refused:
                        m.WaitingFor = null;
                        return TaskStatus.Failure;
                }

                m.WaitingFor = null;

                // Scored before use: using it changes what the score reads (anti-repeat, cooldown)
                float score = context.TraceEnabled ? abilities.Score(id, context) : 0f;

                if (!abilities.TryUse(id, context))
                {
                    ReleaseToken(context);
                    return TaskStatus.Failure;
                }

                m.Used = id;
                if (context.TraceEnabled)
                    context.Record(new DecisionRecord(context.LocalTime, DecisionKind.AbilityUsed, abilityId: id,
                        score: score));
                if (!WaitForFinish)
                {
                    ReleaseToken(context);
                    return TaskStatus.Success;
                }
            }

            if (!abilities.IsRunning(m.Used))
                return TaskStatus.Success;

            // The body is the ability's while it plays
            if (context.Arbiter.TryClaimBoth(this, MoveAuthority.Frozen) && faces &&
                abilities.PhaseOf(m.Used) == AbilityPhase.WindUp)
                context.Actuator?.Face(context.ToTarget);

            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            Mem m = Memory<Mem>(context);
            if (reason != ExitReason.Finished && m.Used != null)
                context?.Abilities?.Cancel();

            ReleaseToken(context);
            context?.Arbiter.Release(this);
        }

        private TokenResult AskToken(AgentContext context, AbilityDefinition definition, bool aimed)
        {
            Mem m = Memory<Mem>(context);
            if (definition == null || string.IsNullOrEmpty(definition.TokenPool))
                return TokenResult.Granted;

            m.Pool = definition.TokenPool;
            return AbilityPlaces.Ask(context, definition, aimed);
        }

        private void ReleaseToken(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            if (m.Pool == null || context == null)
                return;

            context.ReleaseToken(m.Pool);
            m.Pool = null;
        }
    }
}
