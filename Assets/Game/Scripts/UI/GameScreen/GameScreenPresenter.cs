using Game.Scripts.UI.Equipment.Game.Equipment;
using Game.Scripts.UI.GameScreen;
using Inventories;
using UnityEngine;

namespace Inventories
{
    public class GameScreenPresenter : MonoBehaviour
    {
        [SerializeField] private GameScreenView _view;
        [SerializeField] private ItemConsumer _itemConsumer;
        [SerializeField] private InventoryBootstrap _mainInventoryPrefab;
        [SerializeField] private EquipmentBootstrap _equipmentPrefab;

        private InventoryPresenter _inventoryPresenter;
        private EquipmentPresenter _equipmentPresenter;

        private void Start()
        {
            InventoryBootstrap mainInventory = _view.CreateMainInventory(_mainInventoryPrefab);
            _inventoryPresenter = mainInventory.Presenter;

            var equipment = _view.CreateEquipment(_equipmentPrefab);
            _equipmentPresenter = equipment.Presenter;
            
            equipment.Initialize(_itemConsumer);
            equipment.gameObject.SetActive(false);

            _inventoryPresenter.SetEquipment(equipment.Presenter);
        }

        private void OnEnable() => _view.OnInventoryButtonClicked += ToggleInventory;
        private void OnDisable() => _view.OnInventoryButtonClicked -= ToggleInventory;

        private void ToggleInventory()
        {
            _inventoryPresenter.Toggle(_itemConsumer.InventoryPresenter);
            _equipmentPresenter.Toggle();
        }
    }
}