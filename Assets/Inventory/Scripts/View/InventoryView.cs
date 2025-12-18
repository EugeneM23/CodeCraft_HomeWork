using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Inventories
{
    public class InventoryView : MonoBehaviour
    {
        public event Action OnReorganize;

        [SerializeField] private CellView _cellPrefab;
        [SerializeField] private InventoryItem _itemContainerPrefab;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private Vector2 _cellSize = new(100f, 100f);
        [SerializeField] private Button _reorganizeButton;

        private readonly Dictionary<string, InventoryItem> _inventoryItems = new();
        private CellView[,] _cells;
        private Inventory _inventory;
        private InventoryFactory _factory;

        public CellView[,] Cells => _cells;
        public Vector2 CellSize => _cellSize;

        private void OnEnable()
        {
            _reorganizeButton.onClick.AddListener(OnReorganizeClick);
        }

        private void OnDisable()
        {
            _reorganizeButton.onClick.RemoveListener(OnReorganizeClick);
        }

        private void OnReorganizeClick() => OnReorganize?.Invoke();

        public void Initialize(int columns, int rows, Inventory inventory, InventoryFactory factory)
        {
            _factory = factory;
            _inventory = inventory;
            _cells = new CellView[columns, rows];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    CreateCell(inventory, x, y);
                }
            }
        }

        public void AddItem(ItemInstance instance, Vector2Int[] positions)
        {
            InventoryItem inventoryItem = _factory.SpawnItem(_itemContainerPrefab, _gridContainer);
            inventoryItem.transform.position = _cells[positions[0].x, positions[0].y].transform.position;

            inventoryItem.SetupItem(instance, _cellSize, _inventory);

            _inventoryItems[instance.ID] = inventoryItem;

            foreach (Vector2Int pos in positions)
            {
                _cells[pos.x, pos.y].InventoryItem = inventoryItem;
            }
        }

        public void RemoveItem(string id)
        {
            InventoryItem item = _inventoryItems[id];

            foreach (CellView cell in _cells)
            {
                if (cell.InventoryItem == item)
                    cell.Clear();
            }

            _factory.DeSpawn(_inventoryItems[id].gameObject);
            _inventoryItems.Remove(id);
        }

        public CellView GetCell(Vector2Int cellIndex)
        {
            return _cells[cellIndex.x, cellIndex.y];
        }

        private void CreateCell(Inventory inventory, int x, int y)
        {
            CellView cell = _factory.SpawnItem(_cellPrefab, _gridContainer);
            cell.Construct(inventory, new Vector2Int(x, y));

            RectTransform rect = cell.GetComponent<RectTransform>();
            rect.sizeDelta = _cellSize;
            rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

            _cells[x, y] = cell;
        }

        public void Clear()
        {
            foreach (InventoryItem item in _inventoryItems.Values)
                _factory.DeSpawn(item.gameObject);

            _inventoryItems.Clear();

            foreach (CellView cell in _cells)
                cell.Clear();
        }
    }
}