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
        public void Construct(InventoryAdapterN adapter, DiContainer container)
        {
            _adapter = adapter;
            _container = container;
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
            // Помечаем все занятые ячейки
            foreach (var pos in positions)
                _cells[pos.x, pos.y].Item = item;

            // Создаем визуальное представление предмета
            var instance = _container.InstantiatePrefab(_inventoryItemPrefab, _gridContainer);
            var itemRect = instance.GetComponent<RectTransform>();

            // Устанавливаем размер на основе размера предмета
            itemRect.sizeDelta = new Vector2(
                item.itemData.Size.x * _cellSize.x,
                item.itemData.Size.y * _cellSize.y);

            // Позиционируем в первой занятой ячейке
            var firstCellRect = _cells[positions[0].x, positions[0].y].GetComponent<RectTransform>();
            itemRect.anchoredPosition = firstCellRect.anchoredPosition;

            var inventoryItem = instance.GetComponent<InventoryItemView>();
            inventoryItem.Setup(item.itemData);

            // Сохраняем ссылку на view
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
            _draggableImage.rectTransform.position = eventData.position + _dragContext.DragOffset;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var cell = GetCellUnderPointer(eventData);

            transform.SetAsLastSibling();

            if (cell != null && cell.Item != null)
            {
                Vector2Int itemStartPosition = _adapter.GetItemPosition(cell.Item.ID);

                _dragContext.Item = cell.Item;
                _dragContext.StartPosition = itemStartPosition;
                _dragContext.ClickOffset = cell.MatrixPosition - itemStartPosition;
                _dragContext.DragOffset = (Vector2)_items[cell.Item.ID].transform.position - eventData.position;

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
            var cell = eventData.pointerCurrentRaycast.gameObject?.GetComponent<Cell>();

            if (cell != null)
            {
                Vector2Int targetPosition = cell.MatrixPosition - _dragContext.ClickOffset;

                if (cell.Adapter.AddItem(_dragContext.Item, targetPosition))
                {
                    _draggableImage.gameObject.SetActive(false);
                    _dragContext = default;
                    return;
                }
            }

            _adapter.AddItem(_dragContext.Item, _dragContext.StartPosition);
            _draggableImage.gameObject.SetActive(false);
            _dragContext = default;
        }

        private struct DragContext
        {
            public Item Item;
            public Vector2Int StartPosition;
            public Vector2Int ClickOffset;
            public Vector2 DragOffset;
        }
    }
}