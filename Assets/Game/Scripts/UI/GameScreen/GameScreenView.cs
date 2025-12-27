using System;
using Game.Scripts.UI.Equipment.Game.Equipment;
using Inventories;
using UnityEngine;
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
        
        private RectTransform _mainInventoryRect;

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

        public InventoryBootstrap CreateMainInventory(InventoryBootstrap mainInventoryPrefab)
        {
            var installer = Instantiate(mainInventoryPrefab, _mainInventoryRoot);
            installer.Initialize(_inventoryFactory, _dragFSM);
            _dragFSM.SetMainInventory(installer.Inventory);
            _itemConsumer.SetInventory(installer.Inventory);
            installer.Inventory.Owner = _itemConsumer;
            
            _mainInventoryRect = installer.GetComponent<RectTransform>();
            
            return installer;
        }

        public EquipmentBootstrap CreateEquipment(EquipmentBootstrap equipmentPrefab)
        {
            var equipment = Instantiate(equipmentPrefab, _mainInventoryRect);
            return equipment;
        }

        public InventoryBootstrap CreateSecondInventory(InventoryBootstrap secondInventoryPrefab)
        {
            var installer = Instantiate(secondInventoryPrefab, _secondInventoryRoot);
            installer.Initialize(_inventoryFactory, _dragFSM);
            return installer;
        }
    }
}