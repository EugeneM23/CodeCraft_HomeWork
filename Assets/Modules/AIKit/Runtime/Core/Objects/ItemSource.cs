using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// A store that hands out things — an ammo depot giving a box to everyone who comes, a pile of rocks giving a rock
    /// (a smart object that offers Take, to everybody at once: nobody claims a store). Each take makes a new thing from
    /// the one to hand out (a Pickable Item, tagged like the store) and gives it as that thing says: a box is carried in
    /// the hands, a rock with a Weapon is taken up as that weapon. Stock below 0 — it never runs out.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AI Kit/Item Source")]
    public sealed class ItemSource : WorldObject
    {
        [Tooltip("What it hands out (a prefab or an object of the scene to copy; a rock with a Weapon is taken up as " +
                 "that weapon); empty — a plain box")]
        [SerializeField] private GameObject _item;

        [Tooltip("How many it can hand out; below 0 — no end")]
        [SerializeField] private int _stock = -1;

        [Tooltip("Seconds the taking lasts")]
        [Min(0f)] public float TakeTime = 0.5f;

        [Tooltip("The action the body shows while it takes a thing (a state of its animator); empty — none")]
        [ActionName] public string TakeAction = "";

        [Tooltip("Metres from the store a character takes from it")]
        [Min(0.05f)] public float Arrive = 1.2f;

        /// <summary>How many are left; below 0 — no end.</summary>
        public int Stock
        {
            get => _stock;
            set => _stock = value;
        }

        public GameObject Item
        {
            get => _item;
            set => _item = value;
        }

        public override float Reach => Arrive;

        /// <summary>A store serves everybody at once: nobody claims it.</summary>
        public override bool OneAtATime(Offer offer) => false;

        /// <summary>It has things left and the user can take what it hands out.</summary>
        public override bool Offers(Offer offer, AgentContext user) =>
            offer == Offer.Take && IsLive && _stock != 0 && PickableItem.CanTake(user, WeaponOf(_item));

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
            if (!Offers(Offer.Take, interaction.User))
                return TaskStatus.Failure;              // run out in the last moment

            PickableItem made = HandOut();
            interaction.Item = made.transform;
            if (made.GiveTo(interaction.User))
                return TaskStatus.Success;

            Items.UseUp(made.transform);                // could not be taken after all: it goes back into the store
            if (_stock >= 0)
                _stock++;
            return TaskStatus.Failure;
        }

        public override void End(Interaction interaction, ExitReason reason) =>
            EndAction(interaction, reason != ExitReason.Finished);

        /// <summary>
        /// A new thing out of the store, at the store, tagged like it (one put down is found the way the store is); one
        /// fewer left. The store must have some (Stock not 0).
        /// </summary>
        public PickableItem HandOut()
        {
            if (_stock > 0)
                _stock--;

            GameObject made = _item != null ? Instantiate(_item) : Box();
            made.name = _item != null ? _item.name : "Box";
            made.SetActive(true);
            made.tag = gameObject.tag;
            if (!made.TryGetComponent(out PickableItem pickable))
                pickable = made.AddComponent<PickableItem>();

            made.transform.position = transform.position;
            return pickable;
        }

        // The weapon a thing of the store gives (null — it is carried)
        private static WeaponAsset WeaponOf(GameObject item) =>
            item != null && item.TryGetComponent(out PickableItem pickable) ? pickable.Weapon : null;

        // A thing with no model of its own: a small box
        private static GameObject Box()
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.localScale = new Vector3(0.5f, 0.35f, 0.4f);
            return box;
        }
    }
}
