using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Scripts.UI.Chest
{
    public class Chest : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private InventoryInstaller _inventoryPrefab;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private DragFSM _dragFSM;
        [SerializeField] private InventoryFactory _inventoryFactory;

        public void OnPointerClick(PointerEventData eventData)
        {
            InventoryInstaller inventory = Instantiate(_inventoryPrefab, _canvas.transform);
            inventory.Initialize(_inventoryFactory, _dragFSM);
        }
    }
}