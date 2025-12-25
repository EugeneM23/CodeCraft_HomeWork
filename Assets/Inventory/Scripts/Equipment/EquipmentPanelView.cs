using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Scripts.UI.Equipment.Game.Equipment.View
{
    public class EquipmentPanelView : MonoBehaviour
    {
        [SerializeField] private EquipmentSlotView _weaponSlot;
        [SerializeField] private EquipmentSlotView _bodySlot;
        [SerializeField] private EquipmentSlotView _headSlot;
        [SerializeField] private EquipmentSlotView _itemSlot01;
        [SerializeField] private EquipmentSlotView _itemSlot02;
        [SerializeField] private EquipmentSlotView _itemSlot03;

        [SerializeField] private Button _showEquipmentButton;

        public EquipmentSlotView WeaponSlot => _weaponSlot;
        public EquipmentSlotView BodySlot => _bodySlot;
        public EquipmentSlotView HeadSlot => _headSlot;
        public EquipmentSlotView ItemSlot01 => _itemSlot01;
        public EquipmentSlotView ItemSlot02 => _itemSlot02;
        public EquipmentSlotView ItemSlot03 => _itemSlot03;

        public Button ShowEquipmentButton => _showEquipmentButton;
    }
}