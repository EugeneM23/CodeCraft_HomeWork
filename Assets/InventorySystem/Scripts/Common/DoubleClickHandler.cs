using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public class DoubleClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public event Action OnDoubleClick;
        [SerializeField] private float doubleClickTime = 0.3f;

        private ItemUseCase _itemUseCase;
        private Inventory _inventory;

        private float lastClickTime = 0f;
        private ItemInstance _itemInstance;

        public void OnPointerClick(PointerEventData eventData)
        {
            float timeSinceLastClick = Time.time - lastClickTime;

            if (timeSinceLastClick <= doubleClickTime) 
                OnDoubleClick?.Invoke();

            lastClickTime = Time.time;
        }
    }
}