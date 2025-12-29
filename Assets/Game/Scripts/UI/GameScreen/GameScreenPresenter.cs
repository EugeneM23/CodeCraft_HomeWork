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
        [SerializeField] private InventoryFactory _factory;
        [SerializeField] private DragFSM _dragFSM;

        private EquipmentPresenter _equipmentPresenter;

        private Entity _currentSelectedUnit;

        private void OnEnable() => _view.OnInventoryButtonClicked += ToggleInventory;
        private void OnDisable() => _view.OnInventoryButtonClicked -= ToggleInventory;

        private void ToggleInventory()
        {
            if (_currentSelectedUnit.InventoryPresenter == null)
            {
                InventoryBootstrap mainInventory = _view.CreateInventory(_currentSelectedUnit.InventoryPrefab);
                mainInventory.Construct(_factory, _dragFSM);
                _currentSelectedUnit.SetPresenter(mainInventory.Presenter);
                _dragFSM.SetMainInventory(mainInventory.Presenter);
            }

            _currentSelectedUnit.InventoryPresenter.Show();
        }

        public void ShowInventoryButton()
        {
            _view.Show();
        }

        public void HideInventoryButton()
        {
            _view.Hide();
        }

        public void SetCurrentUnit(Entity entity)
        {
            _currentSelectedUnit = entity;
        }
    }
}