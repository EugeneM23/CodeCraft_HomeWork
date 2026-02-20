using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventories
{
    public partial class InventoryView
    {
        public void OnDrag(PointerEventData eventData)
        {
            if (_draggableImage != null && _dragContext.IsDragging)
            {
                _draggableImage.rectTransform.position = eventData.position + _dragContext.DragOffset;

                Cell cell = GetCellUnderPointer(eventData);
                _dragContext.UpdateCurrentCell(cell);
                return;
            }

            DragWindow(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var cell = GetCellUnderPointer(eventData);

            transform.SetAsLastSibling();

            if (cell != null && cell.Item != null)
            {
                Vector2Int itemStartPosition = _adapter.GetItemPosition(cell.Item.ID);
                Vector2Int clickOffset = cell.MatrixPosition - itemStartPosition;
                Vector2 dragOffset = (Vector2)_items[cell.Item.ID].transform.position - eventData.position;

                _dragContext.BeginDrag(cell.Item, itemStartPosition, clickOffset, dragOffset);

                _draggableImage.rectTransform.sizeDelta = new Vector2(
                    cell.Item.itemData.Size.x * _cellSize.x,
                    cell.Item.itemData.Size.y * _cellSize.y);

                _draggableImage.gameObject.SetActive(true);
                _draggableImage.sprite = cell.Item.itemData.Icon;

                _adapter.RemoveItem(cell.Item);
                return;
            }

            BeginWindowDrag(eventData);
        }

        private Cell GetCellUnderPointer(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _gridContainer as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            int x = Mathf.FloorToInt(localPoint.x / _cellSize.x);
            int y = Mathf.FloorToInt(-localPoint.y / _cellSize.y);

            if (x >= 0 && x < _adapter.Width && y >= 0 && y < _adapter.Height)
                return _cells[x, y];

            return null;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragContext.IsDragging)
                return;

            var cell = eventData.pointerCurrentRaycast.gameObject?.GetComponent<Cell>();

            if (cell != null)
            {
                Vector2Int targetPosition = cell.MatrixPosition - _dragContext.ClickOffset;

                if (cell.Adapter.AddItem(_dragContext.Item, targetPosition))
                {
                    _draggableImage.gameObject.SetActive(false);
                    _dragContext.EndDrag();
                    return;
                }
            }

            _adapter.AddItem(_dragContext.Item, _dragContext.StartPosition);
            _draggableImage.gameObject.SetActive(false);
            _dragContext.EndDrag();
        }
    }
}