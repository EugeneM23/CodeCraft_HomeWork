using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// Any object a character may look for — a house, a work place, a well, a patrol zone (step 90; handbook §24.1): put
    /// this marker on it and give it a kind ("Home", "Work", "Patrol Zone"), and a search finds it by that kind — any
    /// prefab with an AI Object is searched without code. It offers Occupy: the character comes and stands at it, facing
    /// the way the object looks. Its owners (the base's Owners) say whose it is: a villager's own house is found by its
    /// "mine" searches, a public well by everybody. A zone (a radius or a box seen from above) makes it an area: Patrol
    /// Area walks within it, the Zone shape of Detection watches it.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Kit/AI Object")]
    public sealed class AIObject : WorldObject
    {
        [Tooltip("What the object is: a search for this kind finds it (chosen from the kinds used in the project, or a new one)")]
        [ObjectKind] public string Kind = "";

        [Tooltip("Metres from the object a character counts as standing at it")]
        [Min(0.05f)] public float Arrive = 0.6f;

        [Tooltip("How many characters may stand at it at once; 0 — any number")]
        [Min(0)] public int Capacity;

        [Tooltip("The object is an area: a circle of this radius around it, seen from above (0 — no circle)")]
        [Min(0f)] public float ZoneRadius;

        [Tooltip("The object is an area: a box this wide (x) and deep (z) around it, turned with it, seen from above " +
                 "(zero — no box; a box wins over a radius)")]
        public Vector2 ZoneSize;

        public override float Reach => Arrive;

        /// <summary>Somebody may stand at it: there is room (Capacity), or the user stands there already.</summary>
        public override bool Offers(Offer offer, AgentContext user) =>
            offer == Offer.Occupy && (Capacity <= 0 || IsOccupiedBy(user) || OccupantCount < Capacity);

        public override bool OneAtATime(Offer offer) => Capacity == 1;

        public override bool Begin(Interaction interaction)
        {
            if (!Offers(Offer.Occupy, interaction.User))
                return false;

            TakeOccupant(interaction.User, interaction.Threat);
            return true;
        }

        /// <summary>Taken: it turns the way the object looks (or to the threat, when it has one) and is done.</summary>
        public override TaskStatus Tick(Interaction interaction, float dt)
        {
            Vector3 toward = interaction.Threat != null
                ? interaction.Threat.position
                : interaction.User.Position + Flat(transform.forward);
            Face(interaction, toward);
            return TaskStatus.Success;
        }

        // ---------- the zone ----------

        /// <summary>The object is an area (a box or a circle).</summary>
        public bool HasZone => ZoneSize.x > 0f && ZoneSize.y > 0f || ZoneRadius > 0f;

        /// <summary>A place within the zone (two numbers of the agent's random, 0…1); no zone — the object itself.</summary>
        public Vector3 RandomPoint(float randomX, float randomZ)
        {
            Vector3 center = transform.position;
            if (ZoneSize.x > 0f && ZoneSize.y > 0f)
            {
                var local = new Vector3((randomX - 0.5f) * ZoneSize.x, 0f, (randomZ - 0.5f) * ZoneSize.y);
                return center + Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * local;
            }

            if (ZoneRadius <= 0f)
                return center;

            // A disc: the root keeps the points even over its area instead of crowding the middle
            float angle = randomX * Mathf.PI * 2f;
            float distance = Mathf.Sqrt(randomZ) * ZoneRadius;
            return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
        }

        /// <summary>The point stands within the zone seen from above, or no further than margin outside it.</summary>
        public bool Contains(Vector3 point, float margin)
        {
            Vector3 offset = point - transform.position;
            if (ZoneSize.x > 0f && ZoneSize.y > 0f)
            {
                Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, transform.eulerAngles.y, 0f)) * offset;
                return Mathf.Abs(local.x) <= ZoneSize.x * 0.5f + margin && Mathf.Abs(local.z) <= ZoneSize.y * 0.5f + margin;
            }

            if (ZoneRadius <= 0f)
                return false;

            offset.y = 0f;
            float reach = ZoneRadius + margin;
            return offset.sqrMagnitude <= reach * reach;
        }

        private void OnDrawGizmosSelected()
        {
            if (!HasZone)
                return;

            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            if (ZoneSize.x > 0f && ZoneSize.y > 0f)
            {
                Matrix4x4 previous = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(ZoneSize.x, 0.05f, ZoneSize.y));
                Gizmos.matrix = previous;
                return;
            }

            const int SEGMENTS = 48;
            Vector3 center = transform.position;
            Vector3 last = center + new Vector3(ZoneRadius, 0f, 0f);
            for (int i = 1; i <= SEGMENTS; i++)
            {
                float angle = i * Mathf.PI * 2f / SEGMENTS;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ZoneRadius;
                Gizmos.DrawLine(last, next);
                last = next;
            }
        }
    }

    /// <summary>
    /// A text field that names a kind of AI Object ("Home", "Work"): the editor offers the kinds the project's AI Objects
    /// use (and lets a new one be typed on an AI Object itself).
    /// </summary>
    public sealed class ObjectKindAttribute : PropertyAttribute
    {
    }
}
