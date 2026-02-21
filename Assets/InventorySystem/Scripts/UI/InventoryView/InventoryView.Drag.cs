using System;
using System.Collections.Generic;
using Inventories;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public partial class InventoryView : IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private Image _draggableImage;
        [Inject] private readonly DragContext _dragContext;
        [Inject] private readonly DragChainProcessor _itemDragProcessor;

        #region Drag

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Поднимаем окно инвентаря на передний план при начале драга
            transform.SetAsLastSibling();

            // Пытаемся начать драг предмета через цепочку обработчиков
            bool success = _itemDragProcessor.ProcessBeginDrag(eventData);

            // Если драг предмета не удался, начинаем драг самого окна
            if (!success)
                BeginWindowDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Пытаемся переместить драгаемый предмет
            if (TryMoveItem(eventData))
                return;

            // Если предмет не драгается, перемещаем само окно инвентаря
            DragWindow(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Обрабатываем завершение драга через цепочку обработчиков (попытка поместить предмет)
            _itemDragProcessor.ProcessEndDrag(eventData);

            // Скрываем визуальный образ драгаемого предмета
            _draggableImage.gameObject.SetActive(false);
    
            // Обнуляем контекст
            _dragContext.Reset();
        }

        #endregion

        #region AdditionalMethods

        public void SetupDraggableImage(Item item)
        {
            _draggableImage.rectTransform.sizeDelta = new Vector2(
                item.itemData.Size.x * _cellSize.x,
                item.itemData.Size.y * _cellSize.y);

            _draggableImage.sprite = item.itemData.Icon;
            _draggableImage.gameObject.SetActive(true);
        }

        private bool TryMoveItem(PointerEventData eventData)
        {
            if (_draggableImage != null && _dragContext.IsDragging)
            {
                _draggableImage.rectTransform.position = eventData.position + _dragContext.DragOffset;

                var cell = eventData.pointerCurrentRaycast.gameObject.GetComponent<InventoryCell>();
                _dragContext.UpdateCurrentCell(cell);
                return true;
            }

            return false;
        }

        #endregion
    }
}