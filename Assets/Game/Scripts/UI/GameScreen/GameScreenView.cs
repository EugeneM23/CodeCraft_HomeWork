using System;
using Inventories;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game.Scripts.UI.GameScreen
{
    public class GameScreenView : MonoBehaviour
    {
        public event Action OnInventoryButtonClicked;

        [SerializeField] private Button _openInventoryButton;
        [SerializeField] private RectTransform _mainInventoryRoot;
        [SerializeField] private RectTransform _secondInventoryRoot;

        [SerializeField] private InventoryFactory _inventoryFactory;
        [SerializeField] private DragFSM _dragFSM;
        [SerializeField] private ItemConsumer _itemConsumer;

        private void OnEnable()
        {
            _openInventoryButton.onClick.AddListener(HandleInventoryButtonClick);
        }

        private void OnDisable()
        {
            _openInventoryButton.onClick.RemoveListener(HandleInventoryButtonClick);
        }

        private void HandleInventoryButtonClick()
        {
            OnInventoryButtonClicked?.Invoke();
        }

        public InventoryInstaller CreateMainInventory(InventoryInstaller mainInventoryPrefab)
        {
            var installer = Instantiate(mainInventoryPrefab, _mainInventoryRoot);
            installer.Initialize(_inventoryFactory, _dragFSM);
            _dragFSM.SetMainInventory(installer.Inventory);
            _itemConsumer.SetInventory(installer.Inventory);
            installer.Inventory.Owner = _itemConsumer;

            return installer;
        }

        public EquipmentInstaller CreateEquipment(EquipmentInstaller equipmentPrefab)
        {
            var equipment = Instantiate(equipmentPrefab, _mainInventoryRoot);
            _itemConsumer.SetEquipment(equipment.Equipment);
            return equipment;
        }

        public InventoryInstaller CreateSecondInventory(InventoryInstaller secondInventoryPrefab)
        {
            var installer = Instantiate(secondInventoryPrefab, _secondInventoryRoot);
            installer.Initialize(_inventoryFactory, _dragFSM);
            return installer;
        }
    }
}