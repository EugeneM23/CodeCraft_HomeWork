using Inventories;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Scripts.UI.Chest
{
    public class Chest : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private InventoryInstaller _inventoryPrefab;
        [SerializeField] private Canvas _canvas;

        public void OnPointerClick(PointerEventData eventData)
        {
            InventoryInstaller installer = Instantiate(_inventoryPrefab, _canvas.transform);
            installer.Initialize(null);
        }
    }
}