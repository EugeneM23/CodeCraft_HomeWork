using Game.Scripts.UI.GameScreen;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Scripts.UI.Chest
{
    public class Chest : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameScreenView _gameScreenView;
        [SerializeField] private InventoryBootstrap _inventoryPrefab;
        [SerializeField] private InventoryFactory _factory;
        [SerializeField] private DragFSM _dragFSM;
        [SerializeField] private CharacterSelector _characterSelector;

        private InventoryPresenter _chestInventoryPresenter;

        public void OnPointerClick(PointerEventData eventData)
        {
            Entity currentEntity = _characterSelector.SelectedEntity;

            if (currentEntity == null)
            {
                Debug.LogWarning("No entity selected! Select a character first.");
                return;
            }

            if (_chestInventoryPresenter == null)
                CreateChestInventory();

            if (currentEntity.InventoryPresenter != null)
            {
                _chestInventoryPresenter.UpdateMainInventory(currentEntity.InventoryPresenter);
                _chestInventoryPresenter.Show(currentEntity.InventoryPresenter);
            }
            else
            {
                Debug.LogWarning("Entity has no inventory presenter!");
            }
        }

        private void CreateChestInventory()
        {
            InventoryBootstrap bootstrap = _gameScreenView.CreateInventory(_inventoryPrefab);
            bootstrap.Construct(_factory, _dragFSM, null);
            _chestInventoryPresenter = bootstrap.Presenter;
        }
    }
}