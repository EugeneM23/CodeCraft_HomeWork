using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Something to hide behind — a crate, a low wall (handbook §18а): it offers Occupy, one shooter at a time, and its
    /// place for a shooter is on the far side from the threat, Behind metres from its middle (the place goes round the
    /// cover as the threat walks round it). Taken, the shooter stands there facing the threat and is "in cover" while
    /// the cover still stands between them; the threat walked round it — it is in cover no more (§18а.2), and its search
    /// for cover holds again. For now a cover is conditional (step 87): no rays check that it hides and lets one shoot
    /// (§18а.1 — those come with crouching and leaning out).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Kit/Cover")]
    public sealed class Cover : WorldObject
    {
        [Tooltip("Metres behind the cover's middle the shooter stands")]
        [Min(0f)] public float Behind = 1.2f;

        [Tooltip("Metres from its place the shooter counts as standing there")]
        [Min(0.05f)] public float Arrive = 0.5f;

        [Tooltip("Degrees off the line from the threat through the cover the shooter may stand and still be hidden")]
        [Range(5f, 90f)] public float ShieldAngle = 40f;

        public override float Reach => Arrive;

        /// <summary>Free (nobody else stands behind it) and taken against a threat.</summary>
        public override bool Offers(Offer offer, AgentContext user) => offer == Offer.Occupy && !IsOccupiedByAnother(user);

        /// <summary>On the far side from the threat, Behind metres from the middle; no threat — behind its own back.</summary>
        public override Vector3 PlaceFor(Offer offer, AgentContext user, Transform threat)
        {
            Vector3 away = threat != null ? Flat(transform.position - threat.position) : Vector3.zero;
            if (away.sqrMagnitude < 1e-6f)
                away = -Flat(transform.forward);
            if (away.sqrMagnitude < 1e-6f)
                away = Vector3.back;
            return transform.position + away.normalized * Behind;
        }

        public override bool Begin(Interaction interaction)
        {
            if (!Offers(Offer.Occupy, interaction.User))
                return false;

            TakeOccupant(interaction.User, interaction.Threat);
            return true;
        }

        /// <summary>Taken: it turns to the threat and is done (it stays behind the cover until it walks off).</summary>
        public override TaskStatus Tick(Interaction interaction, float dt)
        {
            if (interaction.Threat != null)
                Face(interaction, interaction.Threat.position);
            return TaskStatus.Success;
        }

        /// <summary>
        /// The cover still hides the user from the threat it took it against: the user stands on the far side, no more
        /// than Shield Angle off the line from the threat through the cover. The threat gone — it can be judged no more:
        /// still hidden.
        /// </summary>
        public bool Shields(AgentContext user)
        {
            if (user == null || !IsOccupiedBy(user))
                return false;

            Transform threat = ThreatOf(user);
            if (threat == null)
                return true;

            Vector3 line = Flat(transform.position - threat.position);
            Vector3 me = Flat(user.Position - transform.position);
            if (line.sqrMagnitude < 1e-6f || me.sqrMagnitude < 1e-6f)
                return true;

            return Vector3.Angle(line, me) <= ShieldAngle;
        }

        /// <summary>The shooter stays at the cover while it is near it — on any side (the threat may walk round) — and about.</summary>
        protected override bool StillThere(AgentContext user, Transform threat)
        {
            if (!IsAwake(user))
                return false;

            Vector3 to = Flat(user.Position - transform.position);
            float near = Behind + Arrive + LEAVE_SLACK;
            return to.sqrMagnitude <= near * near;
        }
    }
}
