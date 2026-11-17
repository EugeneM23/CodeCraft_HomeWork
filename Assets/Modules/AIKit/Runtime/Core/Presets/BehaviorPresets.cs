using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Ready behaviors so an agent works before anybody opens a graph: pick a preset, tune a handful of numbers, done.
    /// Each preset is a graph like any drawn on the canvas — the Senses sensor writes what the character perceives into
    /// Target, the Brain chooses among decisions, each decision runs its chain of blocks (BrainGraphBuilder). The attack is
    /// Use Any Ability over the tuning's abilities (Ability nodes wired into its set — step 90): taken when one of them is
    /// ready, it picks one and its chain uses it. Read them as examples of how a graph is put together, and copy one as
    /// the starting point of your own.
    /// </summary>
    public static class BehaviorPresets
    {
        public enum Preset
        {
            /// <summary>Patrols, chases what it sees, strikes in melee, recovers, closes in again.</summary>
            MeleeChaser,

            /// <summary>Patrols, keeps a distance band, shoots from it, backs off when crowded.</summary>
            RangedKiter,

            /// <summary>Walks a route and fights only what comes to it — never chases.</summary>
            PatrolGuard,

            /// <summary>Does not move at all: fires at whatever enters its band.</summary>
            Sentry
        }

        /// <summary>
        /// The numbers a preset needs; everything else is the abilities' and the body's. Every one is read by a preset
        /// (invariant 12). Gone with the recheck after step 90 (they did nothing any more; scenes that still hold them
        /// drop them when saved): the sight range of before stage 7 (sight is the Senses sensor's profile), and Attack
        /// Range — the distance it strikes from is its abilities' own range now.
        /// </summary>
        [Serializable]
        public sealed class Tuning
        {
            [Tooltip("The abilities it fights with (the set of its Use Any Ability): when one is ready, it is used. " +
                     "Empty — it never attacks (the preset says so when it is built)")]
            public List<AbilityAsset> Abilities = new();

            [Tooltip("Distance it tries to hold while closing in")]
            [Min(0f)]
            public float PreferredRange = 1.3f;

            [Tooltip("Ranged: do not let the target closer than this")]
            [Min(0f)]
            public float KeepMin = 4f;

            [Tooltip("Ranged: do not let the target farther than this")]
            [Min(0f)]
            public float KeepMax = 8f;

            [Tooltip("Non-empty — the agent attacks only from a place the target gives (its attack places): with none free it " +
                     "waits round the target (Circle) instead of crowding in; empty — nothing limits its attacks")]
            public string TokenPool = "";

            [Tooltip("Pause after an attack, while another of its abilities is ready")]
            [Min(0f)]
            public float Recovery = 0.4f;

            [Tooltip("Seconds of standing at a patrol point")]
            [Min(0f)]
            public float PauseAtPoint = 1f;

            [Tooltip("Route in world space; empty — the agent paces around where it woke up")]
            public Vector3[] PatrolPoints = Array.Empty<Vector3>();

            /// <summary>A copy for one agent: the route and the abilities are copied too, so the copy never writes back.</summary>
            public Tuning Clone()
            {
                var copy = (Tuning)MemberwiseClone();
                copy.PatrolPoints = PatrolPoints != null ? (Vector3[])PatrolPoints.Clone() : Array.Empty<Vector3>();
                copy.Abilities = Abilities != null ? new List<AbilityAsset>(Abilities) : new List<AbilityAsset>();
                return copy;
            }
        }

        /// <summary>The variable the Senses sensor writes and the Brain thinks about.</summary>
        public const string TARGET = "Target";

        public static BehaviorGraph Build(Preset preset, Tuning tuning)
        {
            tuning ??= new Tuning();
            return preset switch
            {
                Preset.RangedKiter => RangedKiter(tuning),
                Preset.PatrolGuard => PatrolGuard(tuning),
                Preset.Sentry => Sentry(tuning),
                _ => MeleeChaser(tuning)
            };
        }

        /// <summary>
        /// Use Any Ability — one of its abilities is ready: it is used, a pause; Chase — sees the target: closes in; with a
        /// token pool, Circle — no place to attack from yet: waits round the target; Patrol — the route.
        /// </summary>
        public static BehaviorGraph MeleeChaser(Tuning tuning)
        {
            BrainGraphBuilder g = Seeing();
            bool places = !string.IsNullOrEmpty(tuning.TokenPool);
            AgentState fight = g.Decision(Fight(tuning, places, nameof(MeleeChaser)));
            g.Chain(fight, new UseAbilityTask(), new WaitTask(tuning.Recovery));
            AgentState chase = g.Decision(new ChaseReasoner
            {
                Condition = places ? new AttackSlotCondition(AttackSlotIs.Mine, tuning.TokenPool) : null
            });
            g.Chain(chase, new ChaseTask { StopDistance = tuning.PreferredRange });
            if (places)
            {
                // No place yet: it waits round the target, and its On Tick asks for one in the group Fight and Chase
                // ask about (step 90: visible blocks instead of Circle's own Ask For Place)
                AgentState circle = g.Decision(new CircleReasoner
                {
                    Condition = new AttackSlotCondition(AttackSlotIs.NotMine, tuning.TokenPool)
                });
                g.Chain(circle, new CircleTargetTask { Radius = tuning.KeepMax, WaitForSlot = false });
                g.OnTick(circle, g.Block(new TakeAttackSlotTask { Pool = tuning.TokenPool, WaitTime = 0f }));
            }

            Patrol(g, tuning);
            return g.Build();
        }

        /// <summary>
        /// Use Any Ability — one of its abilities is ready: Kite into the band (backs off when it is too close), then the
        /// ability and a pause; Attack — the target within the band's far edge, nothing ready (resting, or too near for
        /// the ability's own range): Kite keeps the band — it backs off — and it stands facing the target; Chase — sees it
        /// farther: closes in to the band; Patrol — the route. (The Attack decision holds on the distance alone, so the
        /// band is kept whatever the abilities say — as the kiter did before step 90.)
        /// </summary>
        public static BehaviorGraph RangedKiter(Tuning tuning)
        {
            BrainGraphBuilder g = Seeing();
            AgentState fight = g.Decision(Fight(tuning, false, nameof(RangedKiter)));
            g.Chain(fight, Kite(tuning), new UseAbilityTask(), new WaitTask(tuning.Recovery));
            AgentState band = g.Decision(new FightReasoner { Closer = tuning.KeepMax, LetGoAfter = tuning.KeepMax + SLACK });
            g.Chain(band, Kite(tuning), new StandTask { Time = KEEP_BAND_STAND });
            AgentState chase = g.Decision(new ChaseReasoner());
            g.Chain(chase, new ChaseTask { StopDistance = tuning.KeepMax * 0.9f });
            Patrol(g, tuning);
            return g.Build();
        }

        /// <summary>Metres beyond a distance at which a decision taken lets go (two thresholds: no flicker, handbook §17).</summary>
        private const float SLACK = 0.4f;

        /// <summary>Seconds a kiter stands in its band, facing the target, while nothing of its own is ready.</summary>
        private const float KEEP_BAND_STAND = 0.5f;

        private static KiteTask Kite(Tuning tuning) =>
            new() { MinDistance = tuning.KeepMin, MaxDistance = tuning.KeepMax, NeedsSight = false };

        /// <summary>
        /// Use Any Ability — one of its abilities is ready: it is used, a pause; Watch — sees the target out of reach:
        /// turns to it and holds its ground (never chases); Patrol — the route.
        /// </summary>
        public static BehaviorGraph PatrolGuard(Tuning tuning)
        {
            BrainGraphBuilder g = Seeing();
            AgentState fight = g.Decision(Fight(tuning, false, nameof(PatrolGuard)));
            g.Chain(fight, new UseAbilityTask(), new WaitTask(tuning.Recovery));
            AgentState watch = g.Decision(new WatchReasoner());
            g.Chain(watch, new TurnToTargetTask(), new StandTask { Time = 0.5f });
            Patrol(g, tuning);
            return g.Build();
        }

        /// <summary>Use Any Ability — one of its abilities is ready: it is used, a short pause; otherwise it stands.</summary>
        public static BehaviorGraph Sentry(Tuning tuning)
        {
            BrainGraphBuilder g = Seeing();
            AgentState fire = g.Decision(Fight(tuning, false, nameof(Sentry)));
            g.Chain(fire, new UseAbilityTask(), new WaitTask(0.5f));
            AgentState idle = g.Always();
            g.Chain(idle, new StandTask { Time = 0.5f });
            return g.Build();
        }

        // The attack: Use Any Ability over the tuning's abilities, each an Ability node wired into its set. places — the
        // agent attacks only from a place the target gave (the Attack Slot condition wired into the decision). With no
        // abilities the decision is never taken: a graph made in code is not checked, so the preset says it itself
        private static UseAnyAbilityReasoner Fight(Tuning tuning, bool places, string preset)
        {
            var fight = new UseAnyAbilityReasoner();
            if (tuning.Abilities != null)
            {
                foreach (AbilityAsset ability in tuning.Abilities)
                {
                    if (ability != null)
                        fight.Abilities.Add(new AbilityValue(ability));
                }
            }

            if (fight.Abilities.Count == 0)
                Debug.LogWarning($"AI Kit: the preset {preset} was built with no abilities in its Tuning — its Use Any " +
                                 "Ability is never taken, so the character never attacks. Add the abilities it fights " +
                                 "with to Tuning.Abilities.");

            if (places)
                fight.Condition = new AttackSlotCondition(AttackSlotIs.Mine, tuning.TokenPool);
            return fight;
        }

        // The Senses sensor writes what the character perceives into Target; the Brain thinks about it
        private static BrainGraphBuilder Seeing()
        {
            var g = new BrainGraphBuilder();
            g.Sensor(new SensesSensorTask { StoreIn = TARGET });
            g.BrainTask.Target = TARGET;
            g.Variable(TARGET, VariableType.Transform);
            return g;
        }

        private static void Patrol(BrainGraphBuilder g, Tuning tuning)
        {
            AgentState patrol = g.Decision(new PatrolReasoner());
            g.Chain(patrol, new PatrolTask(tuning.PatrolPoints)
            {
                PauseAtPoint = tuning.PauseAtPoint,
                PauseAtPointMax = tuning.PauseAtPoint * 2f
            });
        }
    }
}
