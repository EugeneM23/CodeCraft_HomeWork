using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class Cell : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Inject] private readonly Inventory _inventory;

        private Item _item;

        public void SetItem(Item item) => _item = item;

        public void OnDrag(PointerEventData eventData)
        {
            if (_item != null)
            {
                Debug.Log(_item.ID);
            }
            else
            {
                Debug.Log("null");
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _inventory.RemoveItem(_item.ID);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }
    }
}