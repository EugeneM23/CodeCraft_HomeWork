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
        [SerializeField] private EquipmentInstaller _equipmentPrefab;

        private InventoryPresenter _inventoryPresenter;
        private EquipmentPresenter _equipmentPresenter;

        private void Start()
        {
            InventoryInstaller installer = _view.CreateMainInventory(_mainInventoryPrefab);
            _inventoryPresenter = installer.Presenter;

            EquipmentInstaller equipment = _view.CreateEquipment(_equipmentPrefab);
            equipment.Initialize(itemConsumer);

            _equipmentPresenter = new EquipmentPresenter(equipment.Equipment);
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
            _equipmentPresenter.Toggle();
        }
    }
}