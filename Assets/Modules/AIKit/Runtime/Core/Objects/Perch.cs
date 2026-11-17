using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// High ground a character can climb onto and fight from (a block, a ledge; handbook §13а.2 — a shooter up there
    /// reads as dangerous): a smart object that offers Occupy, one character at a time; its place is the middle of its
    /// top, and anywhere on the top counts as there. The way up is the level's — climbing links (the Climb area) from
    /// the ground to its top: Climb To takes the character up, Interact occupies it. Its pivot stands at its foot, in the
    /// middle; the top is Height above it, a square Half Size each way (or the Top object, when one is given).
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Kit/Perch")]
    public sealed class Perch : WorldObject
    {
        [Tooltip("Metres from the foot (the pivot) to the top")]
        [Min(0f)] public float Height = 2.5f;

        [Tooltip("Half the width of the top, metres, each way from its middle")]
        [Min(0.1f)] public float HalfSize = 1.5f;

        [Tooltip("The middle of the top (an empty object up there); empty — Height above the pivot")]
        public Transform Top;

        /// <summary>The middle of the top: where a character climbing up goes to.</summary>
        public Vector3 TopCenter => Top != null ? Top.position : transform.position + Vector3.up * Height;

        /// <summary>Anywhere on the top is there.</summary>
        public override float Reach => HalfSize;

        /// <summary>Nobody else stands on it.</summary>
        public override bool Offers(Offer offer, AgentContext user) =>
            offer == Offer.Occupy && IsLive && !IsOccupiedByAnother(user);

        public override Vector3 PlaceFor(Offer offer, AgentContext user, Transform threat) => TopCenter;

        /// <summary>Up there it occupies the perch; still on the ground — it is not there yet.</summary>
        public override bool Begin(Interaction interaction)
        {
            if (!Offers(Offer.Occupy, interaction.User) || !IsOn(interaction.User.Position))
                return false;

            TakeOccupant(interaction.User, interaction.Threat);
            return true;
        }

        /// <summary>Taken: it turns to the threat and is done (it stays up there until it climbs down).</summary>
        public override TaskStatus Tick(Interaction interaction, float dt)
        {
            if (interaction.Threat != null)
                Face(interaction, interaction.Threat.position);
            return TaskStatus.Success;
        }

        /// <summary>The character stands on the top (and is about: a fallen one is up there no more).</summary>
        protected override bool StillThere(AgentContext user, Transform threat) => IsAwake(user) && IsOn(user.Position);

        /// <summary>The point stands on the top (within it, flat, and up there — not under it).</summary>
        public bool IsOn(Vector3 point)
        {
            Vector3 top = TopCenter;
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0f, transform.eulerAngles.y, 0f)) * (point - top);
            return point.y >= top.y - 0.6f && Mathf.Abs(local.x) <= HalfSize + 0.15f && Mathf.Abs(local.z) <= HalfSize + 0.15f;
        }
    }
}
