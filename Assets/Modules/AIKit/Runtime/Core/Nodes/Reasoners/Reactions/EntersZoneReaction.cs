using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Enters Zone: the Brain's target steps into a zone of the world — an AI Object of the kind Zone that has an area (a
    /// guarded yard, a camp, a door) — whose owners Whose allows (the user's order of 13.11: "someone enters a zone"; step
    /// 90: the zones are objects of the world now, the character keeps no scene objects). Caught on the step in (the
    /// rising edge, handbook §38 №15); taken, it holds while the target stays within the zone with Slack metres, and lets
    /// go when it has left (§17: a line in space is decided with slack). The zones come from the world's registry (no scan
    /// of the scene, §24).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Decisions/Reactions/Enters Zone", "Task", "aikit.reasoner.enters-zone", Simple = true, Endless = true)]
    public sealed class EntersZoneReaction : ReactionDecision
    {
        /// <summary>The kind of AI Object the zone is ("Yard", "Camp"); empty — any AI Object with an area.</summary>
        [ObjectKind] public string Zone = "";

        /// <summary>Whose zones are watched: mine (a guard's own yard), mine or public, anyone's.</summary>
        public Ownership Whose = Ownership.MineOrPublic;

        /// <summary>Metres the target must be out of the zone before it counts as gone (no flicker on the edge).</summary>
        [Min(0f)] public float Slack = 0.5f;

        // The look's list: shared by every agent of the graph, used within one Observe at a time (one thread)
        [NonSerialized] private static readonly List<WorldObject> Zones = new();

        public EntersZoneReaction()
        {
            Cooldown = 3f;
        }

        public override bool NeedsTarget => true;

        public override string Requirement =>
            base.Requirement + (string.IsNullOrEmpty(Zone)
                ? "; zones — any AI Object with an area"
                : $"; zones — AI Objects of the kind {Zone}");

        protected override bool KeepsWhileInCharge(ReactionMemory memory) => memory.Latched;

        protected override bool Happens(AgentContext context, in TargetFacts facts, ReactionMemory memory, bool explain,
            out bool fresh)
        {
            fresh = false;
            Transform target = facts.Target;
            if (target == null)
            {
                memory.Latched = false;
                memory.WhyNot = "no target";
                return false;
            }

            memory.Latched = InAZone(context, target.position, memory.Latched ? Slack : 0f);
            if (memory.Latched)
                return true;

            memory.WhyNot = explain
                ? $"{target.name} is in no zone{(string.IsNullOrEmpty(Zone) ? "" : " of the kind " + Zone)}"
                : "target in no zone";
            return false;
        }

        /// <summary>The point is within a zone this reaction watches (with the margin), as far as Whose allows.</summary>
        public bool InAZone(AgentContext context, Vector3 point, float margin)
        {
            WorldObjects registry = context?.WorldObjects;
            if (registry == null)
                return false;

            Zones.Clear();
            registry.Find(new ObjectQuery { Component = typeof(AIObject), Kind = Zone }, Zones);
            AICharacter agent = context.Agent;
            int allowed = WorldObjects.AllowedTier(Whose, Zones, agent);
            for (int i = 0; i < Zones.Count; i++)
            {
                if (Zones[i] is AIObject zone && zone.HasZone && WorldObjects.TierOf(zone, agent) <= allowed &&
                    zone.Contains(point, margin))
                    return true;
            }

            return false;
        }
    }
}
