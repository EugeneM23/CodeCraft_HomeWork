using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class InventoryViewN : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private Cell _cellPrefab;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private Vector2Int _cellSize;

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
                    // Создаем ячейку из prefab
                    var cell = _container.InstantiatePrefab(_cellPrefab, _gridContainer);

                    // Настраиваем размер и позицию
                    var rect = cell.GetComponent<RectTransform>();
                    rect.sizeDelta = _cellSize;
                    rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

                    // Сохраняем ссылку и задаем позицию в матрице
                    var cellComponent = cell.GetComponent<Cell>();
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

            // Сохраняем ссылку на view
            _items[item.ID] = instance;
        }

        private void RemoveItem(Item item, Vector2Int[] positions)
        {
            Debug.Log($"Removing item {item.ID}");
            _items[item.ID].gameObject.SetActive(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }
    }
}