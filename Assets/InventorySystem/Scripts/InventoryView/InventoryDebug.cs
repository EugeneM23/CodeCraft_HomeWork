using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryDebug : MonoBehaviour
    {
        [Inject] DiContainer _container;

        [SerializeField] private InventoryView _inventoryPrefab;
        [SerializeField] private Canvas _canvas;
        private GameObject _inventory;

        [Button]
        public void CreateInventory()
        {
            _inventory = _container.InstantiatePrefab(_inventoryPrefab, _canvas.transform);
        }

        [Button]
        public void RemoveItem(Vector2Int position)
        {
            var inventory = _inventory.GetComponent<GameObjectContext>().Container.Resolve<Inventory>();
            Debug.Log(inventory == null);
            bool removeItem = inventory.RemoveItem(position);

            Debug.Log(removeItem);
        }

        [Button]
        public void AddItem(SceneItem item)
        {
            var inventory = _inventory.GetComponent<GameObjectContext>().Container.Resolve<Inventory>();
            inventory.AddItem(item.ItemData);
        }
    }
}