using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Senses: the character's sight as the handbook builds it (§9.1–9.6: focus, periphery and back cones around where
    /// the body looks, awareness that grows and fades, memory of the last place, the alert ladder, what an ally's shout
    /// or a hit makes known). It is the one owner of those senses (step 90, C21: they are no section of the character
    /// any more): it opens them for the character when the graph starts — their state is this sensor's, for this one
    /// agent — and closes them when the graph stops; a character whose graph has no Senses sensor has no senses at all.
    /// What it sees with is its one setting, Profile (a Perception Profile asset; empty — the defaults). Every Interval it
    /// writes into its variable the target the character knows (aware of it), nothing otherwise. Wire the variable into
    /// the Brain's Target: the decisions then think about what the character perceives. The body's eyes find the
    /// candidates (its scanner); the character's side is nobody's target (its Faction).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Senses/Senses", "Sensor", "aikit.sensor.senses", Order = 80, WorksAlone = true)]
    public sealed class SensesSensorTask : SensorTask
    {
        private sealed class Senses
        {
            public AgentPerception Perception;
        }

        /// <summary>What the character sees with: its cones, memory and alert thresholds (empty — the defaults).</summary>
        [NonSerialized] public PerceptionProfile Profile;

        public SensesSensorTask()
        {
            Tag = "";
            Interval = 0.1f;
        }

        protected internal override float Reach(AgentContext context) => 0f;

        protected internal override bool Sees(AgentContext context, Transform candidate, bool isSeen) => false;

        /// <summary>The target the character's perception holds and knows now (null — none).</summary>
        public static Transform Known(AgentContext context)
        {
            if (context == null || !context.Track.IsValid)
                return null;

            bool knows = context.Perception == null || context.Perception.Level == AlertLevel.Aware;
            return knows ? context.Track.Transform : null;
        }

        /// <summary>The perception this sensor opened for the agent (null — it has not started for it).</summary>
        public AgentPerception PerceptionOf(AgentContext context) =>
            context != null ? OwnMemory<Senses>(context).Perception : null;

        /// <summary>
        /// Gives this one agent a profile of its own, set in code (a spawner's own sight per fighter): its Senses sensor
        /// sees with it instead of the sensor's own Profile. Null — the sensor's own again. Before or after the graph
        /// starts.
        /// </summary>
        public static void SetProfile(AICharacter character, PerceptionProfile profile)
        {
            if (character == null)
                return;

            character.Initialize();
            character.Context.SensesProfile = profile;
            if (profile != null && character.Context.Perception != null)
                character.Context.Perception.Profile = profile;
        }

        private PerceptionProfile ProfileFor(AgentContext context) =>
            context.SensesProfile != null ? context.SensesProfile : Profile;

        // The graph starts: the character's senses open — one perception per agent, kept in this sensor's memory and
        // registered on the agent (its shouts heard, its hits felt, the character steps it before every think). A body
        // with no eyes (no scanner) opens none: its target is what the game hands over (SetTarget), as ever
        protected override void OnEnter(AgentContext context)
        {
            base.OnEnter(context);
            if (context == null || context.Scanner == null)
                return;

            Senses senses = OwnMemory<Senses>(context);
            senses.Perception ??= new AgentPerception(context, ProfileFor(context), context.Scanner, context.TargetFilter);
            senses.Perception.Profile = ProfileFor(context);
            senses.Perception.Scanner = context.Scanner;
            senses.Perception.Filter = context.TargetFilter;
            if (context.Perception == null)
                context.Perception = senses.Perception;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context != null)
            {
                AgentPerception perception = OwnMemory<Senses>(context).Perception;
                if (perception != null && context.Perception == perception)
                {
                    perception.Profile = ProfileFor(context);     // a profile given in code since counts at once
                    context.Agent?.World?.ReportAlert(perception.Level);
                }
            }

            return base.OnTick(context, dt);
        }

        // The graph stops: the senses close with it
        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (context == null)
                return;

            AgentPerception perception = OwnMemory<Senses>(context).Perception;
            if (perception != null && context.Perception == perception)
                context.Perception = null;
        }

        // A look every Interval, as every sensor: what perception knows now
        protected override Transform Look(AgentContext context, Transform seen) => Known(context);
    }
}
