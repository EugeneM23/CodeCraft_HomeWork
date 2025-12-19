using Inventories;
using UnityEngine;

namespace Game.Scripts.GameSystem
{
    public class GameContext : MonoBehaviour
    {
        [SerializeField] private TestCharacter _testCharacter;
        [SerializeField] private InventoryInstaller _inventoryPrefab;
        [SerializeField] private InventoryFactory _factory;
        [SerializeField] private DragFSM _dragFSM;
        [SerializeField] private Canvas _canvas;

        private void Start()
        {
            InventoryInstaller inventory = Instantiate(_inventoryPrefab, _canvas.GetComponent<RectTransform>());
            inventory.Initialize(_factory, _dragFSM, _testCharacter);
            _testCharacter.Inventory = inventory.Inventory;

            _dragFSM.SetMainInventory(inventory.Inventory);
        }

        private void OnEnable()
        {
        }
    }
}