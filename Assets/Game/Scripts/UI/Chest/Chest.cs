using Game.Scripts.UI.GameScreen;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Scripts.UI.Chest
{
    public class Chest : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameScreenView _mainScreenView;
        [SerializeField] private InventoryBootstrap _inventoryPrefab;
        [SerializeField] private InventoryFactory _factory;
        [SerializeField] private DragFSM _dragFsm;

        private InventoryPresenter _chestInventoryPresenter;
        private Entity _currentEntity;

        public void SetCurrentEntity(Entity entity)
        {
            _currentEntity = entity;
            
            if (_chestInventoryPresenter != null && _currentEntity?.InventoryPresenter != null)
                _chestInventoryPresenter.UpdateMainInventory(_currentEntity.InventoryPresenter);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_chestInventoryPresenter == null)
                CreateChestInventory();

            if (_currentEntity?.InventoryPresenter != null)
                _chestInventoryPresenter.Show(_currentEntity.InventoryPresenter);
        }

        private void CreateChestInventory()
        {
            InventoryBootstrap inventoryBootstrap = _mainScreenView.CreateInventory(_inventoryPrefab);
            inventoryBootstrap.Construct(_factory, _dragFsm);
            _chestInventoryPresenter = inventoryBootstrap.Presenter;
        }
    }
}