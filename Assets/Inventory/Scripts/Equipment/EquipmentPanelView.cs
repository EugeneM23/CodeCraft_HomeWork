using UnityEngine;

namespace Game.Scripts.UI.Equipment.Game.Equipment.View
{
    public class EquipmentPanelView : MonoBehaviour
    {
        [SerializeField] private EquipmentSlotView _weaponSlot01;
        [SerializeField] private EquipmentSlotView _weaponSlot02;
        [SerializeField] private EquipmentSlotView _headSlot;
        [SerializeField] private EquipmentSlotView _bodySlot;
        [SerializeField] private EquipmentSlotView _legsSlot;
        [SerializeField] private EquipmentSlotView _bootsSlot;
        [SerializeField] private EquipmentSlotView _handsSlot;
        [SerializeField] private EquipmentSlotView _itemSlot;

        public EquipmentSlotView WeaponSlot01 => _weaponSlot01;
        public EquipmentSlotView WeaponSlot02 => _weaponSlot02;
        public EquipmentSlotView BodySlot => _bodySlot;
        public EquipmentSlotView HeadSlot => _headSlot;
        public EquipmentSlotView LegsSlot => _legsSlot;
        public EquipmentSlotView BootsSlot => _bootsSlot;
        public EquipmentSlotView HandsSlot => _handsSlot;

        public EquipmentSlotView ItemSlot => _itemSlot;
    }
}