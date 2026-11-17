using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Finds an object of the world (step 90, C15 — "how to search"): what to look for comes by a wire from a What node,
    /// the found object goes out by a wire into a Transform variable (Store In) for the blocks after it (Move To, Interact).
    /// Three ways to search:
    /// - Nearest — of the objects the world knows (the registry, never a scan of the scene), the nearest that fits: at
    ///   once;
    /// - Look Around — the same, of those its eyes see (a clear line from the eyes — the body's sight probe): at once;
    /// - Wander — runs to random points around where it began until its eyes see one; gives up after Give Up After.
    /// What fits: what the What node asks (its kind, what the object offers the character now — the object's own say —,
    /// whose it is, how far), not claimed by another character, not one this character found no way to a moment ago
    /// (the unreachable journal, handbook §24). Of those, its own before public ones, then the nearest; one it already
    /// holds keeps it while it still fits (§18а.2 p.4). The found object is claimed for the chain — the others look for
    /// another — until the chain lets it go (its decision leaves) or stops using it for a while.
    /// Success ("Found"), Failure ("Nothing Found": the variable is emptied; the search decision rests).
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Objects/Find", "Task", "aikit.task.find", Success = "Found", Failure = "Nothing Found", Simple = true)]
    public sealed class FindTask : AgentTask
    {
        public enum Way
        {
            /// <summary>Of the objects the world knows, the nearest that fits: at once.</summary>
            Nearest,

            /// <summary>Of the objects its eyes see, the nearest that fits: at once.</summary>
            LookAround,

            /// <summary>Runs to random points until its eyes see one that fits; gives up after a while.</summary>
            Wander
        }

        /// <summary>Seconds between two looks while it wanders.</summary>
        public const float LOOK_EVERY = 0.25f;

        /// <summary>Metres from a point of the wander it counts as there.</summary>
        private const float ARRIVE = 0.6f;

        /// <summary>Metres above an object's foot the eyes look at.</summary>
        private const float LOOK_AT = 0.5f;

        private sealed class Mem
        {
            public float StartedAt;
            public float LookedAt = float.NegativeInfinity;
            public Vector3 Origin;
            public Vector3 Point;
            public bool HasPoint;
        }

        /// <summary>What to search for: the What node wired in. No wire — nothing to search for (a problem of the graph).</summary>
        [NonSerialized] public WhatValue What;

        /// <summary>How it searches.</summary>
        public Way How = Way.Nearest;

        /// <summary>The Transform variable the found object is written to (the wire out to the blocks that use it).</summary>
        [VariableName(VariableType.Transform)] public string StoreIn;

        /// <summary>Wander: metres around where it began it runs about.</summary>
        public BlackboardValue<float> WanderRadius = 12f;

        /// <summary>Wander: seconds it searches before it gives up.</summary>
        public BlackboardValue<float> GiveUpAfter = 20f;

        /// <summary>Wander: how fast it runs, as a part of the body's full speed.</summary>
        public BlackboardValue<float> Pace = 1f;

        // One search at a time (the main thread): the registry's candidates
        private static readonly List<WorldObject> Candidates = new();

        // Shared by every agent of the graph: caches the name's id only, never a value
        [NonSerialized] private BlackboardVariable<Transform> _store;

        public override bool IsInstant => How != Way.Wander;

        public override string Requirement => How == Way.Wander
            ? "claims what it finds; runs about until it sees one"
            : "claims what it finds: the others look for another";

        /// <summary>The object this agent's variable holds now (what the last search found); null — none.</summary>
        public Transform FoundBy(AgentContext context) =>
            context != null && !string.IsNullOrEmpty(StoreIn) ? _store.Get(context.Blackboard, StoreIn) : null;

        protected override void OnEnter(AgentContext context)
        {
            Mem m = Memory<Mem>(context);
            m.StartedAt = context?.LocalTime ?? 0f;
            m.LookedAt = float.NegativeInfinity;
            m.Origin = context?.Position ?? Vector3.zero;
            m.HasPoint = false;
        }

        protected override TaskStatus OnTick(AgentContext context, float dt)
        {
            if (context == null || What == null)
                return NothingFound(context);

            if (How != Way.Wander)
                return Take(context, Search(context, How == Way.LookAround)) ? TaskStatus.Success : NothingFound(context);

            Mem m = Memory<Mem>(context);
            float now = context.LocalTime;
            if (now - m.LookedAt >= LOOK_EVERY)
            {
                m.LookedAt = now;
                if (Take(context, Search(context, true)))
                {
                    context.Navigation?.Clear();
                    return TaskStatus.Success;
                }
            }

            if (now - m.StartedAt >= GiveUpAfter.Get(context))
                return NothingFound(context);

            Wander(context, m, dt);
            return TaskStatus.Running;
        }

        protected override void OnExit(AgentContext context, ExitReason reason)
        {
            if (How != Way.Wander || context == null)
                return;

            if (context.Navigation is INavigationPace pace)
                pace.Pace = 1f;
            context.Navigation?.Clear();
            if (reason != ExitReason.Finished)
                context.Actuator?.Stop();
        }

        /// <summary>
        /// The object that fits best now for the agent (sees — only one its eyes see); null — none. Its own before public
        /// ones before anybody's (as far as What's Whose allows), then the one it holds already, then the nearest.
        /// </summary>
        public WorldObject Search(AgentContext context, bool sees)
        {
            if (context == null || What == null)
                return null;

            WorldObjects registry = context.WorldObjects;
            AICharacter agent = context.Agent;
            ObjectQuery query = What.QueryFor(context);
            registry.Find(query, Candidates);
            int allowed = WorldObjects.AllowedTier(What.Whose, Candidates, agent);
            if (allowed > 0 && What.Whose == Ownership.MineOrPublic && registry.OwnsAny(query, agent))
                allowed = 0;                            // its own lies beyond the radius: none of the others (§24)
            Transform threat = context.BrainTarget;
            Transform held = FoundBy(context);
            Vector3 from = context.Position;
            Vector3 eye = context.EyePosition;

            WorldObject best = null;
            int bestTier = int.MaxValue;
            float bestDistance = float.MaxValue;
            bool bestHeld = false;
            for (int i = 0; i < Candidates.Count; i++)
            {
                WorldObject candidate = Candidates[i];
                int tier = WorldObjects.TierOf(candidate, agent);
                if (tier > allowed || tier > bestTier)
                    continue;

                bool isHeld = held != null && candidate.transform == held;
                if (candidate.OneAtATime(What.Offer) && registry.IsClaimedByAnother(candidate, agent) ||
                    registry.IsUnreachableFor(agent, candidate) || !What.Fits(context, candidate, threat, isHeld))
                    continue;

                if (sees && context.Sight != null &&
                    !context.Sight.HasLineOfSight(eye, candidate.transform.position + Vector3.up * LOOK_AT))
                    continue;

                Vector3 to = candidate.transform.position - from;
                to.y = 0f;
                float distance = to.sqrMagnitude;
                bool better = tier < bestTier || isHeld && !bestHeld || !bestHeld && distance < bestDistance;
                if (!better)
                    continue;

                best = candidate;
                bestTier = tier;
                bestDistance = distance;
                bestHeld = isHeld;
            }

            Candidates.Clear();
            return best;
        }

        // The found object: claimed for the chain (one at a time — the others look for another), written out
        private bool Take(AgentContext context, WorldObject found)
        {
            if (found == null)
                return false;

            if (found.OneAtATime(What.Offer))
            {
                AIWorldRunner.Join(context.Agent);      // a claim runs out on the world's clock
                if (!context.WorldObjects.TryClaim(found, context.Agent))
                    return false;
            }

            if (!string.IsNullOrEmpty(StoreIn))
                _store.Set(context.Blackboard, StoreIn, found.transform);
            return true;
        }

        private TaskStatus NothingFound(AgentContext context)
        {
            if (context == null)
                return TaskStatus.Failure;

            if (!string.IsNullOrEmpty(StoreIn))
                _store.Set(context.Blackboard, StoreIn, null);
            context.NothingFoundAt = context.LocalTime;
            return TaskStatus.Failure;
        }

        // One step of running about: to a random point around where it began, then to the next
        private void Wander(AgentContext context, Mem m, float dt)
        {
            INavigator navigation = context.Navigation;
            if (navigation == null)
                return;

            if (navigation is INavigationPace pace)
                pace.Pace = Pace.Get(context);

            if (m.HasPoint && (!navigation.Status.IsMoving() || navigation.RemainingDistance <= ARRIVE))
                m.HasPoint = false;                     // there, or no way there: the next point

            if (!m.HasPoint)
            {
                float radius = Mathf.Max(0f, WanderRadius.Get(context));
                float angle = (float)context.NextRandom() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt((float)context.NextRandom()) * radius;
                Vector3 point = m.Origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (!navigation.SetDestination(point).CanGo())
                    return;                             // one point per step: a bad one costs a step

                m.Point = point;
                m.HasPoint = true;
            }

            if (Claim(context, BodyAxis.Move, MoveAuthority.Path))
                navigation.Step(dt);
        }
    }
}
