using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class InventoryViewN : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private Cell _cellPrefab;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;
        [SerializeField] private Image _draggableImage;
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private Vector2Int _cellSize;

        private DragContext _dragContext;
        private InventoryAdapterN _adapter;
        private DiContainer _container;

        private Cell[,] _cells;
        private Dictionary<string, GameObject> _items;

        [Inject]
        public void Construct(InventoryAdapterN adapter, DiContainer container, DragContext dragContext)
        {
            _adapter = adapter;
            _container = container;
            _dragContext = dragContext;
            _cells = new Cell[adapter.Width, adapter.Height];
            _items = new Dictionary<string, GameObject>();
        }

        private void OnEnable()
        {
            _adapter.OnItemAdded += CreateItem;
            _adapter.OnItemRemoved += RemoveItem;
        }

        private void OnDisable()
        {
            _adapter.OnItemAdded -= CreateItem;
            _adapter.OnItemRemoved -= RemoveItem;
        }

        private void Start()
        {
            CreateGrid();

            foreach (var kvp in _adapter.GetItems())
                CreateItem(kvp.Key, kvp.Value);
        }

        private void CreateGrid()
        {
            for (int y = 0; y < _adapter.Height; y++)
            {
                for (int x = 0; x < _adapter.Width; x++)
                {
                    var cell = _container.InstantiatePrefab(_cellPrefab, _gridContainer);
                    var cellComponent = cell.GetComponent<Cell>();
                    var rect = cell.GetComponent<RectTransform>();

                    rect.sizeDelta = _cellSize;
                    rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

                    cellComponent.MatrixPosition = new Vector2Int(x, y);

                    _cells[x, y] = cellComponent;
                }
            }
        }

        private void CreateItem(Item item, Vector2Int[] positions)
        {
            foreach (var pos in positions)
                _cells[pos.x, pos.y].Item = item;

            var instance = _container.InstantiatePrefab(_inventoryItemPrefab, _gridContainer);
            var itemRect = instance.GetComponent<RectTransform>();

            itemRect.sizeDelta = new Vector2(
                item.itemData.Size.x * _cellSize.x,
                item.itemData.Size.y * _cellSize.y);

            var firstCellRect = _cells[positions[0].x, positions[0].y].GetComponent<RectTransform>();
            itemRect.anchoredPosition = firstCellRect.anchoredPosition;

            var inventoryItem = instance.GetComponent<InventoryItemView>();
            inventoryItem.Setup(item.itemData);

            _items[item.ID] = instance;
        }

        private void RemoveItem(Item item, Vector2Int[] positions)
        {
            if (_items.ContainsKey(item.ID))
            {
                Destroy(_items[item.ID]);
                _items.Remove(item.ID);
            }

            foreach (var pos in positions)
                _cells[pos.x, pos.y].Item = null;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_draggableImage != null && _dragContext.IsDragging) 
                _draggableImage.rectTransform.position = eventData.position + _dragContext.DragOffset;
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
            }
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