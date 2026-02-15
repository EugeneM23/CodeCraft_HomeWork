using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public class InventoryDebug : MonoBehaviour
    {
        [Inject] DiContainer _container;

        [SerializeField] private InventoryUI _inventoryPrefab;
        [SerializeField] private Canvas _canvas;

        [Button]
        public void CreateInventory()
        {
            GameObject inventory = _container.InstantiatePrefab(_inventoryPrefab, _canvas.transform);
        }
    }
}