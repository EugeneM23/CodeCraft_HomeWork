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
        [SerializeField] private ItemConsumer itemConsumer;
        [SerializeField] private InventoryFactory _factory;
        [SerializeField] private DragFSM _dragFsm;

        private InventoryPresenter _chestInventoryPresenter;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_chestInventoryPresenter == null)
                CreateChestInventory();

            _chestInventoryPresenter.Toggle(itemConsumer.InventoryPresenter);
        }

        private void CreateChestInventory()
        {
            var inventoryBootstrap = _mainScreenView.CreateInventory(_inventoryPrefab);
            inventoryBootstrap.Construct(_factory, _dragFsm);
            _chestInventoryPresenter = inventoryBootstrap.Presenter;
        }
    }
}