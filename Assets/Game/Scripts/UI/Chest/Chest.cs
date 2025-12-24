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

        private InventoryBootstrap _chestInventoryBootstrap;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_chestInventoryBootstrap == null)
                CreateChestInventory();

            Debug.Log(_chestInventoryBootstrap == null);
            _chestInventoryBootstrap.Toggle(itemConsumer.Inventory);
        }

        private void CreateChestInventory()
        {
            _chestInventoryBootstrap = _mainScreenView.CreateSecondInventory(_inventoryPrefab).Bootstrap;
        }
    }
}