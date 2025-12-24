using Game.Scripts.UI.Equipment.Game.Equipment;
using Game.Scripts.UI.GameScreen;
using Inventories;
using UnityEngine;
using UnityEngine.Serialization;

namespace Inventories
{
    public class GameScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameScreenView _view;
        [SerializeField] private ItemConsumer itemConsumer;
        [SerializeField] private InventoryInstaller _mainInventoryPrefab;
        [SerializeField] private EquipmentBootstrap _equipmentPrefab;

        private InventoryBootstrap _inventoryBootstrap;

        private void Start()
        {
            InventoryInstaller installer = _view.CreateMainInventory(_mainInventoryPrefab);
            _inventoryBootstrap = installer.Bootstrap;

            EquipmentBootstrap equipment = _view.CreateEquipment(_equipmentPrefab);
            equipment.Initialize(itemConsumer);
        }

        private void OnEnable()
        {
            _view.OnInventoryButtonClicked += ToggleInventory;
        }

        private void OnDisable()
        {
            _view.OnInventoryButtonClicked -= ToggleInventory;
        }

        private void ToggleInventory()
        {
            _inventoryBootstrap.Toggle(itemConsumer.Inventory);
        }
    }
}