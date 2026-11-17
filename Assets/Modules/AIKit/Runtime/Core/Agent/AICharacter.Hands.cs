using UnityEngine;

namespace AIKit
{
    // What the character holds in its hands (IHands): one thing at a time, in front of the chest
    public sealed partial class AICharacter : IHands
    {
        [Header("Hands")]
        [Tooltip("Where a thing carried in the hands is held (a bone or an empty object of the body); empty — Carry " +
                 "Offset from the character")]
        [SerializeField] private Transform _carryPoint;

        [Tooltip("Where a thing carried in the hands is held, from the character's feet: up to the chest and in front " +
                 "of it (used when there is no Carry Point)")]
        [SerializeField] private Vector3 _carryOffset = new(0f, 1.2f, 0.5f);

        private Transform _held;

        /// <summary>The thing held now; null — the hands are empty (a thing destroyed meanwhile counts as gone).</summary>
        public Transform Item => _held != null ? _held : null;

        public Vector3 CarryOffset
        {
            get => _carryOffset;
            set => _carryOffset = value;
        }

        public bool Hold(Transform item)
        {
            if (item == null || Item != null)
                return false;

            _held = item;
            Transform anchor = _carryPoint != null ? _carryPoint : transform;
            item.SetParent(anchor, false);
            item.localPosition = _carryPoint != null ? Vector3.zero : _carryOffset;
            item.localRotation = Quaternion.identity;
            ShowWeapons();                              // both hands on the load: the weapon goes on the back
            return true;
        }

        public Transform LetGo()
        {
            Transform item = Item;
            _held = null;
            if (item != null)
                item.SetParent(null, true);
            ShowWeapons();                              // the hands free again: the weapon in the hand again
            return item;
        }
    }
}
