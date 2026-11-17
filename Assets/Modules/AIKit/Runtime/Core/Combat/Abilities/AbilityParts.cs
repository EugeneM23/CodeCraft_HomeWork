using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A part an ability is made of (step 90, C6; handbook §16 — an ability is data): a condition of when it may be used,
    /// how it is delivered, what it does where it lands, how it is shown while it flies. The parts are data of the shared
    /// asset — nothing here changes in play (handbook C.5); a character's AbilityRunner runs them.
    /// </summary>
    [Serializable]
    public abstract class AbilityPart
    {
        /// <summary>The part in a few words for the inspector and the canvas ("4–9 m", "rests 6 s").</summary>
        public virtual string Phrase => null;
    }

    // ---------- conditions: when it may be used (asked before a decision is taken — handbook §17: filter first) ----------

    /// <summary>A condition of when the ability may be used: it writes itself into the ability's description.</summary>
    [Serializable]
    public abstract class AbilityCondition : AbilityPart
    {
        /// <summary>The condition in the description the agent's abilities check (AgentAbilities.BlockOf).</summary>
        public abstract void Describe(AbilityDefinition definition);
    }

    /// <summary>From how near to how far from the target.</summary>
    [Serializable]
    public sealed class RangeCondition : AbilityCondition
    {
        [Tooltip("Not nearer to the target than this, metres")]
        [Min(0f)] public float Min;

        [Tooltip("Not farther from the target than this, metres")]
        [Min(0f)] public float Max = 2f;

        // A distance is to a target: an ability with a range needs one, whatever it does
        public override void Describe(AbilityDefinition definition)
        {
            definition.Range = new RangeBand(Min, Mathf.Max(Min, Max));
            definition.RequiresTarget = true;
        }

        public override string Phrase => Min > 0f ? $"{Min:0.#}–{Max:0.#} m" : $"up to {Max:0.#} m";
    }

    /// <summary>Only with a clear line to the target (the body's sight probe).</summary>
    [Serializable]
    public sealed class LineCondition : AbilityCondition
    {
        public override void Describe(AbilityDefinition definition)
        {
            definition.RequiresLineOfSight = true;
            definition.RequiresTarget = true;
        }

        public override string Phrase => "clear line";
    }

    /// <summary>Only from the ground (not in the air).</summary>
    [Serializable]
    public sealed class GroundCondition : AbilityCondition
    {
        public override void Describe(AbilityDefinition definition) => definition.RequiresGround = true;

        public override string Phrase => "on the ground";
    }

    /// <summary>Only with the target in front, at the side or behind the character (a backstab, a turn-around sweep).</summary>
    [Serializable]
    public sealed class AngleCondition : AbilityCondition
    {
        [Tooltip("The target within 45° of where the character looks")]
        public bool Front = true;

        [Tooltip("The target 45–135° from where the character looks")]
        public bool Side;

        [Tooltip("The target more than 135° from where the character looks")]
        public bool Back;

        public override void Describe(AbilityDefinition definition)
        {
            definition.Angles = (Front ? AbilityAngles.Front : 0) | (Side ? AbilityAngles.Side : 0) |
                                (Back ? AbilityAngles.Back : 0);
            definition.RequiresTarget = true;
        }

        public override string Phrase =>
            $"target {(Front ? "in front" : "")}{(Front && (Side || Back) ? ", " : "")}{(Side ? "at the side" : "")}" +
            $"{(Side && Back ? ", " : "")}{(Back ? "behind" : "")}";
    }

    /// <summary>A pause after the ability ENDS before it may be used again (handbook §16: from the end, not the start).</summary>
    [Serializable]
    public sealed class CooldownCondition : AbilityCondition
    {
        [Tooltip("Seconds of rest after the ability ends")]
        [Min(0f)] public float Seconds = 5f;

        public override void Describe(AbilityDefinition definition) => definition.Cooldown = Seconds;

        public override string Phrase => $"rests {Seconds:0.#} s";
    }

    /// <summary>Not before this long after the character wakes up (a pack does not telegraph in unison).</summary>
    [Serializable]
    public sealed class StartDelayCondition : AbilityCondition
    {
        [Tooltip("Seconds after the character wakes up before the first use")]
        [Min(0f)] public float Seconds = 1f;

        public override void Describe(AbilityDefinition definition) => definition.StartDelay = Seconds;

        public override string Phrase => $"not in the first {Seconds:0.#} s";
    }

    /// <summary>
    /// How good the ability is right now, by scoring axes (handbook §4.3–4.4): an axis with no data vetoes it (it is not
    /// ready), the rest multiply its weight among the abilities of a Use Any Ability.
    /// </summary>
    [Serializable]
    public sealed class ScoreCondition : AbilityCondition
    {
        [Tooltip("The scoring axes; a required one with no data vetoes the ability")]
        public Consideration[] Considerations = Array.Empty<Consideration>();

        [Tooltip("How the axes combine")]
        public ScoreReduction Reduction = ScoreReduction.Product;

        public override void Describe(AbilityDefinition definition)
        {
            definition.Considerations = Considerations != null ? (Consideration[])Considerations.Clone()
                : Array.Empty<Consideration>();
            definition.Reduction = Reduction;
        }

        public override string Phrase => $"scored by {Considerations?.Length ?? 0} axes";
    }

    // ---------- the queue and the timing: plain numbers every ability has ----------

    /// <summary>
    /// How the ability takes its turn: among the characters at one target (At Once in its pool — handbook §18.2 p. 1: a
    /// pool of its own per kind of attack) and among the abilities of a Use Any Ability (its weight, and less of it right
    /// after it was used — anti-repeat, §4.6).
    /// </summary>
    [Serializable]
    public sealed class AbilityQueue
    {
        [Tooltip("How many characters use it on one target at the same time; 0 — any number")]
        [Min(0)] public int AtOnce = 1;

        [Tooltip("The pool of places at the target it takes its turn in; empty — a pool of its own (its name). Abilities " +
                 "with one pool share its turns (a grenade and a firebomb: one thrower at a time)")]
        [PoolName] public string Pool = "";

        [Tooltip("How willingly it is picked among the abilities of a Use Any Ability that are ready")]
        [Min(0f)] public float Weight = 1f;

        [Tooltip("How much of its weight it loses when it was the last one used (0 — none, 1 — it is never picked twice " +
                 "in a row while another is ready)")]
        [Range(0f, 1f)] public float RepeatPenalty = 0.5f;
    }

    /// <summary>
    /// The swing and the rest: numbers for now — the fallback a clip will replace once the presentation tells the frame of
    /// the release (handbook §20.3).
    /// </summary>
    [Serializable]
    public sealed class AbilityTiming
    {
        [Tooltip("Seconds from the start until it is released (lands, leaves the hand) — the warning the target gets")]
        [Min(0f)] public float WindUp = 0.8f;

        [Tooltip("Seconds the character stands after the release")]
        [Min(0f)] public float Recovery = 0.6f;
    }

    // ---------- the delivery: how it gets where it acts (one per ability) ----------

    /// <summary>How the ability gets where it acts: a blow in reach, a projectile, a lob to a place, on the user itself.</summary>
    [Serializable]
    public abstract class AbilityDelivery : AbilityPart
    {
        /// <summary>The ability is about a target (a blow, a shot); false — it acts on the user itself.</summary>
        public virtual bool NeedsTarget => true;
    }

    /// <summary>A blow at the release: the effects happen on the target if it is still within Reach.</summary>
    [Serializable]
    public sealed class StrikeDelivery : AbilityDelivery
    {
        [Tooltip("Metres from the character to the target the blow still lands on (centre to centre)")]
        [Min(0f)] public float Reach = 2f;

        public override string Phrase => $"a blow, {Reach:0.#} m";
    }

    /// <summary>
    /// A projectile at the target through the body's launcher — the same flight as a weapon's shot (step 90): straight
    /// (Arc 0, hits what it passes near) or a lob (comes down where the target stood). The effects happen where it lands.
    /// </summary>
    [Serializable]
    public sealed class ProjectileDelivery : AbilityDelivery
    {
        [Tooltip("The projectile's model (no colliders needed); empty — a thin stick")]
        public GameObject Projectile;

        [Tooltip("0 — straight; above 0 — a lob rising this share of its length")]
        [Min(0f)] public float Arc;

        [Tooltip("Metres per second")]
        [Min(1f)] public float Speed = 20f;

        [Tooltip("Where it leaves from, above the feet")]
        public float Height = 1.4f;

        [Tooltip("Where it leaves from, ahead of the body")]
        public float Forward = 0.5f;

        [Tooltip("Where on the target it flies to, above its feet")]
        public float AimHeight = 1.2f;

        [Tooltip("How near it must pass (or come down by) the target to hit it")]
        [Min(0.1f)] public float HitRadius = 0.7f;

        [Tooltip("Metres of free room the flight needs on each side of its line")]
        [Min(0f)] public float Clearance = 0.3f;

        public override string Phrase => Arc > 0f ? "a lobbed projectile" : "a projectile";

        /// <summary>The flight's numbers in the shape the launcher reads.</summary>
        public WeaponShot ToShot() => new()
        {
            Projectile = Projectile, Arc = Arc, Speed = Speed, Height = Height, Forward = Forward, AimHeight = AimHeight,
            HitRadius = HitRadius, Clearance = Clearance, Pool = ""
        };
    }

    /// <summary>
    /// A lob to a place on the ground — where the target stood at the release — through the body's launcher, in a fixed
    /// time in the air (the time the target has to step out: handbook §19.1). The effects happen where it comes down.
    /// </summary>
    [Serializable]
    public sealed class LobDelivery : AbilityDelivery
    {
        [Tooltip("The thrown thing's model; empty — a thin stick")]
        public GameObject Projectile;

        [Tooltip("Seconds in the air: the time the target has to step out of the place")]
        [Min(0.1f)] public float Flight = 1f;

        [Tooltip("How high the arc rises above the straight line, metres")]
        [Min(0f)] public float Arc = 2.2f;

        [Tooltip("Where it leaves from, above the feet")]
        public float Hand = 1.5f;

        public override string Phrase => $"a lob, {Flight:0.#} s in the air";

        /// <summary>The flight's numbers in the shape the launcher reads.</summary>
        public WeaponShot ToShot(float hitRadius) => new()
        {
            Projectile = Projectile, Speed = 10f, Height = Hand, Forward = 0.3f, AimHeight = 0f,
            HitRadius = Mathf.Max(0.1f, hitRadius), Clearance = 0f, Pool = ""
        };
    }

    /// <summary>On the user itself at the release (a heal, a shout, a stance): the effects have the user as their target.</summary>
    [Serializable]
    public sealed class SelfDelivery : AbilityDelivery
    {
        public override bool NeedsTarget => false;

        public override string Phrase => "on itself";
    }

    /// <summary>
    /// The game plays the ability with its own code: an IAbilityRunner it registers on the character under the ability's
    /// Name (handbook §16 — the adapter to what the game has). The conditions and the queue of the asset still choose when;
    /// timing, effects and cues are the runner's.
    /// </summary>
    [Serializable]
    public sealed class CodeRunnerDelivery : AbilityDelivery
    {
        public override string Phrase => "played by the game's code";
    }

    // ---------- the effects: what happens where it acts ----------

    /// <summary>Where an ability acts: who used it, at whom, where, and whom it hit directly.</summary>
    public readonly struct AbilityImpact
    {
        public readonly AgentContext Context;
        public readonly Transform User;

        /// <summary>The target the ability was used at (the user itself for an ability on itself).</summary>
        public readonly Transform Target;

        /// <summary>Where it acts (where the projectile came down, the target's place for a blow).</summary>
        public readonly Vector3 Point;

        /// <summary>Whom it hit directly (a blow in reach, a projectile that struck); null — nobody.</summary>
        public readonly Transform Struck;

        /// <summary>
        /// The delivery decided whom it hit — a blow in reach, a projectile's own flight by its hit radius: Struck is the
        /// whole answer, an effect does not hit by a radius of its own (one owner of the hit). False — a lob to a place:
        /// the effect's radius round where it came down decides.
        /// </summary>
        public readonly bool HitDecided;

        public AbilityImpact(AgentContext context, Transform user, Transform target, Vector3 point, Transform struck,
            bool hitDecided = false)
        {
            Context = context;
            User = user;
            Target = target;
            Point = point;
            Struck = struck;
            HitDecided = hitDecided;
        }
    }

    /// <summary>What happens where the ability acts.</summary>
    [Serializable]
    public abstract class AbilityEffect : AbilityPart
    {
        public abstract void Apply(in AbilityImpact impact);
    }

    /// <summary>
    /// A hit on the target (IHitReceiver): the one the blow or the projectile struck — a projectile that missed by its own
    /// flight hits nobody — or, for a lob to a place, the target if it is within Hit Radius of where it came down.
    /// </summary>
    [Serializable]
    public sealed class DamageEffect : AbilityEffect
    {
        [Tooltip("How hard it hits")]
        public float Damage = 1f;

        [Tooltip("Metres from where a lob came down the target is still hit (a projectile hits by its own hit radius)")]
        [Min(0f)] public float HitRadius = 1f;

        public override void Apply(in AbilityImpact impact)
        {
            Transform victim = impact.Struck;
            if (victim == null && !impact.HitDecided && impact.Target != null)
            {
                Vector3 off = impact.Target.position - impact.Point;
                off.y = 0f;
                if (off.magnitude <= HitRadius)
                    victim = impact.Target;
            }

            if (victim != null)
                DirectStriker.Deliver(impact.User, victim, Damage);
        }

        public override string Phrase => $"damage {Damage:0.#}";
    }

    /// <summary>
    /// A hit on every object of the tag within Radius of where it acts (a blast): the victims are found through the
    /// agent's object finder (AgentContext.Objects — no scan of the world, handbook §24); the user itself is never one.
    /// </summary>
    [Serializable]
    public sealed class AreaDamageEffect : AbilityEffect
    {
        [Tooltip("Metres around the place that are hit")]
        [Min(0.1f)] public float Radius = 2f;

        [Tooltip("How hard each one is hit")]
        public float Damage = 1f;

        [Tooltip("Who can be hit: objects with this tag")]
        [TagName] public string Tag = "Player";

        private static readonly List<Transform> Victims = new();

        public override void Apply(in AbilityImpact impact)
        {
            IObjectFinder finder = impact.Context?.Objects;
            if (finder == null || string.IsNullOrEmpty(Tag))
                return;

            finder.FindAll(Tag, impact.Point, Radius, impact.User, Victims);
            for (int i = 0; i < Victims.Count; i++)
                DirectStriker.Deliver(impact.User, Victims[i], Damage);
            Victims.Clear();
        }

        public override string Phrase => $"blast {Radius:0.#} m, {Damage:0.#} to {Tag}";
    }

    // ---------- the cues: what is shown while it flies (handbook §20.4: a cue never changes the game) ----------

    /// <summary>
    /// What is shown while the ability's projectile flies (a mark where it will come down): begun at the release, ended
    /// where it comes down — and always ended when the ability is cancelled (handbook §20.4: End is mandatory). The asset is
    /// shared: what a cue makes is kept by the character's runner and handed back to End.
    /// </summary>
    [Serializable]
    public abstract class AbilityCue : AbilityPart
    {
        /// <summary>The cue starts at the place it is about; what it made (kept by the runner); null — nothing.</summary>
        public abstract object Begin(in AbilityImpact at);

        /// <summary>The cue ends: what Begin made goes.</summary>
        public abstract void End(object made);
    }

    /// <summary>A mark on the ground where the projectile will come down, as big as the blast (the time to step out).</summary>
    [Serializable]
    public sealed class GroundMarkCue : AbilityCue
    {
        [Tooltip("The mark's radius, metres (the blast's)")]
        [Min(0.1f)] public float Radius = 2f;

        [Tooltip("The mark's colour (the plain disc)")]
        public Color Colour = new(0.75f, 0.12f, 0.1f);

        [Tooltip("A model of the mark, scaled to the radius; empty — a flat disc")]
        public GameObject Mark;

        public override object Begin(in AbilityImpact at)
        {
            GameObject mark;
            if (Mark != null)
            {
                mark = UnityEngine.Object.Instantiate(Mark);
            }
            else
            {
                mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                // The disc's collider goes (by name: the core does not touch the physics — PerceptionTests)
                Component collider = mark.GetComponent("CapsuleCollider");
                if (collider != null && Application.isPlaying)
                    UnityEngine.Object.Destroy(collider);
                else if (collider != null)
                    UnityEngine.Object.DestroyImmediate(collider);
                var colour = new MaterialPropertyBlock();
                colour.SetColor("_Color", Colour);
                colour.SetColor("_BaseColor", Colour);
                mark.GetComponent<Renderer>().SetPropertyBlock(colour);
            }

            mark.name = "Ground Mark";
            mark.transform.position = at.Point + Vector3.up * 0.03f;
            mark.transform.localScale = new Vector3(Radius * 2f, 0.01f, Radius * 2f);
            return mark;
        }

        public override void End(object made)
        {
            if (made is not GameObject mark || mark == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(mark);
            else
                UnityEngine.Object.DestroyImmediate(mark);
        }

        public override string Phrase => $"mark {Radius:0.#} m";
    }
}
