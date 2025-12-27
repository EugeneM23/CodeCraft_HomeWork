using Game.Scripts.UI.GameScreen;
using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace Game.Scripts.UI.Chest
{
    public class Chest : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameScreenView _mainScreenView;
        [SerializeField] private InventoryInstaller _inventoryPrefab;
        [FormerlySerializedAs("_testCharacter")] [SerializeField] private ItemConsumer itemConsumer;

        private InventoryPresenter _chestInventoryPresenter;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_chestInventoryPresenter == null)
                CreateChestInventory();

            Debug.Log(_chestInventoryPresenter == null);
            _chestInventoryPresenter.Toggle(itemConsumer.Inventory);
        }

        private void CreateChestInventory()
        {
            _chestInventoryPresenter = _mainScreenView.CreateSecondInventory(_inventoryPrefab).Presenter;
        }
    }
}