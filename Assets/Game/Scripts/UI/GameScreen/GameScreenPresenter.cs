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

        private InventoryPresenter _inventoryPresenter;

        private void Start()
        {
            InventoryInstaller installer = _view.CreateMainInventory(_mainInventoryPrefab);
            _inventoryPresenter = installer.Presenter;

            EquipmentBootstrap equipment = _view.CreateEquipment(_equipmentPrefab);
            equipment.Initialize(itemConsumer);
            
            _inventoryPresenter.SetEquipment(equipment.Presenter);
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
            _inventoryPresenter.Toggle(itemConsumer.Inventory);
        }
    }
}