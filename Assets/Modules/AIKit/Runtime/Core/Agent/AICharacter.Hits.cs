using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// The character as a receiver of blows (step 90, reactions): one hit path — DirectStriker tells every receiver of
    /// the victim, the character among them, and a game's own damage is another receiver (or calls TakeHit itself when
    /// it does not use DirectStriker). The character keeps the hit in its memory (AgentContext.Hits: who, when, from
    /// where, the pressure of recent hits), marks itself hurt for its presentation (AgentContext.HurtAt / HurtFrom — the
    /// flinch is the body's response, played by the animator, not a decision of the Brain) and thinks on its next step.
    /// What the graph does about it is its own: the Got Hit decision, the Hit By shape of Detection.
    /// </summary>
    public sealed partial class AICharacter : IHitReceiver
    {
        /// <summary>The back half of the character: a blow from farther round than this (degrees) came from behind.</summary>
        public const float BEHIND_ANGLE = 90f;

        void IHitReceiver.ReceiveHit(in HitInfo hit) => TakeHit(hit);

        /// <summary>
        /// A blow landed on the character: remembered (who, when, from where, how hard lately), shown as hurt, the
        /// attacker made known to its senses at once (a Senses sensor's perception, if it has one: Alert), and the
        /// character thinks on its next step instead of waiting out its think interval. Its own blow is not a hit on it.
        /// The module never changes health: that is the game's.
        /// </summary>
        public void TakeHit(in HitInfo hit)
        {
            Initialize();
            if (Context == null)
                return;

            Transform self = EntityRoot != null ? EntityRoot : transform;
            if (hit.Attacker != null && (hit.Attacker == self || hit.Attacker.IsChildOf(self)))
                return;

            Vector3 at = Context.Position;
            Vector3 from = hit.Attacker != null ? hit.Attacker.position : at;
            Context.Hits.Record(hit.Attacker, from, hit.Damage, IsBehind(self.forward, from - at), Context.LocalTime);
            Context.HurtAt = Context.LocalTime;
            Context.HurtFrom = from;

            // Damage always wakes the senses (handbook §9.3): the attacker is known at once, the entity the eyes would
            // find (its IPerceivable), not the hitbox that struck
            if (Context.Perception != null && hit.Attacker != null)
            {
                Transform attacker = hit.Attacker.GetComponentInParent<IPerceivable>() is Component perceived
                    ? perceived.transform
                    : hit.Attacker;
                Context.Perception.Alert(attacker);
            }

            RequestThink();
        }

        /// <summary>
        /// A direction (from the character to something) is behind it: farther round from where it faces than
        /// BEHIND_ANGLE, on the ground's plane. Nothing to measure (on top of it, no facing) — not behind.
        /// </summary>
        public static bool IsBehind(Vector3 forward, Vector3 toward)
        {
            forward.y = 0f;
            toward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f || toward.sqrMagnitude < 1e-6f)
                return false;

            return Vector3.Angle(forward, toward) > BEHIND_ANGLE;
        }
    }
}
