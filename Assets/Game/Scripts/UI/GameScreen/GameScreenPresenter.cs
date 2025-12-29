using System;
using Game.Scripts.UI.GameScreen;
using Inventories;
using UnityEngine;

namespace Inventories
{
    public class GameScreenPresenter : MonoBehaviour
    {
        public event Action<InventoryPresenter> OnInventoryCreated;
        
        [SerializeField] private GameScreenView _view;
        [SerializeField] private InventoryFactory _factory;
        [SerializeField] private DragFSM _dragFSM;

        private Entity _currentSelectedUnit;

        private void OnEnable() => _view.OnInventoryButtonClicked += ToggleInventory;
        private void OnDisable() => _view.OnInventoryButtonClicked -= ToggleInventory;

        private void ToggleInventory()
        {
            Debug.Log($"Toggle inventory for: {_currentSelectedUnit?.name}");
            
            if (_currentSelectedUnit.InventoryPresenter == null)
            {
                Debug.Log($"Creating inventory for {_currentSelectedUnit.name}");
                InventoryBootstrap inventoryBootstrap = _view.CreateInventory(_currentSelectedUnit.InventoryPrefab);
                inventoryBootstrap.Construct(_factory, _dragFSM);
                _currentSelectedUnit.SetInventoryPresenter(inventoryBootstrap.Presenter);
                _dragFSM.SetMainInventory(_currentSelectedUnit.InventoryPresenter);
                OnInventoryCreated?.Invoke(_currentSelectedUnit.InventoryPresenter);
            }

            _currentSelectedUnit.InventoryPresenter.Show();
        }

        public void ShowInventoryButton() => _view.Show();
        public void HideInventoryButton() => _view.Hide();
        
        public void SetCurrentUnit(Entity entity)
        {
            Debug.Log($"SetCurrentUnit called with: {entity?.name}");
            _currentSelectedUnit = entity;
        }

        public Entity GetCurrentUnit() => _currentSelectedUnit;
    }
}