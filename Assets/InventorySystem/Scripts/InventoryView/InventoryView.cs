using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Inventories
{
    public class InventoryView : MonoBehaviour
    {
        public event Action OnReorganizeClicked;
        public event Action OnCollectAllClicked;
        public event Action OnCloseClicked;
        public event Action OnEquipClicked;

        [SerializeField] private CellView _cellPrefab;
        [SerializeField] private InventoryItem _itemPrefab;
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private Vector2 _cellSize = new(100f, 100f);
        
        [SerializeField] private Button _reorganizeButton;
        [SerializeField] private Button _collectAllButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _showEquipmentButton;

        public CellView[,] Cells { get; private set; }
        private readonly Dictionary<string, InventoryItem> _items = new();
        private InventoryFactory _factory;

        private void OnEnable()
        {
            if (_reorganizeButton != null)
                _reorganizeButton.onClick.AddListener(HandleReorganizeClick);

            if (_collectAllButton != null)
                _collectAllButton.onClick.AddListener(HandleCollectAllClick);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(HandleCloseClick);

            if (_showEquipmentButton != null)
                _showEquipmentButton.onClick.AddListener(HandleShowEquipmentClick);
        }

        private void OnDisable()
        {
            if (_reorganizeButton != null)
                _reorganizeButton.onClick.RemoveListener(HandleReorganizeClick);

            if (_collectAllButton != null)
                _collectAllButton.onClick.RemoveListener(HandleCollectAllClick);

            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(HandleCloseClick);

            if (_showEquipmentButton != null)
                _showEquipmentButton.onClick.RemoveListener(HandleShowEquipmentClick);
        }

        public void Initialize(int width, int height, InventoryPresenter presenter, InventoryFactory factory)
        {
            _factory = factory;
            Cells = new CellView[width, height];

            CreateGrid(width, height, presenter);
        }

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
        }

        public void DisplayItem(Item item, Vector2Int[] positions, InventoryPresenter presenter)
        {
            InventoryItem inventoryItem = _factory.SpawnItem(_itemPrefab, _gridContainer);
            inventoryItem.transform.position = Cells[positions[0].x, positions[0].y].transform.position;
            inventoryItem.SetupItem(item, _cellSize, presenter);

            _items[item.ID] = inventoryItem;

            foreach (Vector2Int pos in positions)
                Cells[pos.x, pos.y].InventoryItem = inventoryItem;
        }

        public void RemoveItem(string itemId)
        {
            if (!_items.TryGetValue(itemId, out InventoryItem item)) return;

            foreach (CellView cell in Cells)
            {
                if (cell.InventoryItem == item)
                    cell.Clear();
            }

            _factory.DeSpawn(item.gameObject);
            _items.Remove(itemId);
        }

        public void ClearAllItems()
        {
            foreach (InventoryItem item in _items.Values)
                _factory.DeSpawn(item.gameObject);

            _items.Clear();

            foreach (CellView cell in Cells)
                cell.Clear();
        }

        private void CreateGrid(int width, int height, InventoryPresenter presenter)
        {
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                CreateCell(x, y, presenter);
        }

        private void CreateCell(int x, int y, InventoryPresenter presenter)
        {
            CellView cell = _factory.SpawnItem(_cellPrefab, _gridContainer);
            cell.Construct(presenter, new Vector2Int(x, y));

            RectTransform rect = cell.GetComponent<RectTransform>();
            rect.sizeDelta = _cellSize;
            rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

            Cells[x, y] = cell;
        }

        private void HandleReorganizeClick() => OnReorganizeClicked?.Invoke();
        private void HandleCollectAllClick() => OnCollectAllClicked?.Invoke();
        private void HandleCloseClick() => OnCloseClicked?.Invoke();

        private void HandleShowEquipmentClick() => OnEquipClicked?.Invoke();
    }
}