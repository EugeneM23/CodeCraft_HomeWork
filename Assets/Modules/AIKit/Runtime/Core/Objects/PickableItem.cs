using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A thing lying about that a character can take (handbook §24.1, a smart object that offers Take): a box of ammo — it
    /// is carried in the hands, at the chest; a rock — with a Weapon it is taken up as that weapon (a weapon from the
    /// world, for its Uses: the rock is thrown and gone, the character's own weapon is back in the hand). What taking it
    /// gives is the object's own say — the graph only says Interact. While somebody holds it (or it flies) it offers
    /// nothing; put down, it may be taken again. The core knows no physics (its colliders are not touched): give the thing
    /// none, or put it on a layer the sight does not look through, so a box in the hands does not block its bearer's own
    /// line of sight.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Kit/Pickable Item")]
    public sealed class PickableItem : WorldObject
    {
        [Tooltip("Taking it gives this weapon (a rock: a weapon to throw, with one move): the thing is held as a weapon " +
                 "from the world, the character's own goes on the back. Empty — it is carried in the hands (a box)")]
        public WeaponAsset Weapon;

        [Tooltip("Seconds the taking lasts (the body bends down for it)")]
        [Min(0f)] public float TakeTime = 0.5f;

        [Tooltip("The action the body shows while it takes the thing (a state of its animator); empty — none")]
        [ActionName] public string TakeAction = "";

        private AICharacter _holder;

        /// <summary>
        /// Who holds it now — in the hands, or in the hand as a weapon from the world; null — it lies about (or flies).
        /// Asked, not kept: a thing its holder no longer holds (thrown, used up, put down) is nobody's.
        /// </summary>
        public AICharacter Holder
        {
            get
            {
                if (_holder == null)
                    return null;

                IWeaponRack rack = _holder.Context?.Rack;
                bool held = _holder.Item == transform || rack != null && rack.FromWorld == transform;
                return held ? _holder : null;
            }
        }

        /// <summary>
        /// It is in the air (thrown): nobody may take it until it lands. Set when it is let fly (Projectiles.Launch), off
        /// again when it is put down (Items.PutDown); a thrown rock that lands is gone (C14).
        /// </summary>
        public bool IsFlying { get; set; }

        /// <summary>Lies about, not in the air, and the user can take what it gives (free hands; a free hand for a weapon).</summary>
        public override bool Offers(Offer offer, AgentContext user) =>
            offer == Offer.Take && IsLive && Holder == null && !IsFlying && CanTake(user, Weapon);

        /// <summary>The thing is nearby: the user walks up to it.</summary>
        public override Vector3 PlaceFor(Offer offer, AgentContext user, Transform threat) => transform.position;

        public override bool Begin(Interaction interaction)
        {
            if (!Offers(Offer.Take, interaction.User))
                return false;

            ShowAction(interaction, TakeAction);
            return true;
        }

        public override TaskStatus Tick(Interaction interaction, float dt)
        {
            Face(interaction, transform.position);
            if (interaction.Time < TakeTime)
                return TaskStatus.Running;

            EndAction(interaction, false);
            return GiveTo(interaction.User) ? TaskStatus.Success : TaskStatus.Failure;
        }

        public override void End(Interaction interaction, ExitReason reason) =>
            EndAction(interaction, reason != ExitReason.Finished);

        /// <summary>
        /// The user gets the thing: its weapon taken up from the world (the thing is that weapon's object), or the thing in
        /// the hands. False — it cannot take it now (it is somebody's, the hand or the hands are full).
        /// </summary>
        public bool GiveTo(AgentContext user)
        {
            if (user == null || Holder != null || IsFlying || !CanTake(user, Weapon))
                return false;

            _holder = user.Agent;
            bool taken = Weapon != null
                ? user.Rack.TakeUpFromWorld(Weapon, transform, Weapon.Uses, mounted: false)   // its uses are the weapon's (one owner)
                : user.Hands.Hold(transform);
            if (!taken)
                _holder = null;
            return taken;
        }

        /// <summary>It lies on the ground again, free to be taken (Items.PutDown calls it).</summary>
        public void Release()
        {
            _holder = null;
            IsFlying = false;
        }

        /// <summary>
        /// The user can take a thing that gives this weapon (none — a thing to carry): a weapon needs a hand that holds no
        /// other weapon from the world and no load; a load needs empty hands — no load, and no weapon from the world in the
        /// hand (a rock is put down or thrown first, as a load is put down before a rock is taken).
        /// </summary>
        public static bool CanTake(AgentContext user, WeaponAsset weapon)
        {
            if (user == null || user.Hands != null && user.Hands.Item != null)
                return false;

            bool handFree = user.Rack == null || user.Rack.FromWorld == null;
            return weapon != null ? user.Rack != null && handFree : user.Hands != null && handFree;
        }
    }
}
