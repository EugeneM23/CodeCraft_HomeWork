using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private Button _reorganizeButton;
        [SerializeField] private Button _closeButton;

        [Inject] private InventoryAdapter _adapter;
        [Inject] private DiContainer _container;

        private Cell[,] _cells;
        private Dictionary<Vector2Int, InventoryView> _items = new();

        private void OnEnable()
        {
            _adapter.OnItemAdded += OnItemAdded;
            _adapter.OnItemRemoved += OnItemRemoved;
            _reorganizeButton.onClick.AddListener(_adapter.Reorganize);
        }

        private void OnItemRemoved(Item item, Vector2Int[] positions)
        {
            
        }

        private void Start()
        {
            _cells = new Cell[_adapter.Width, _adapter.Height];
            CreateGrid();
            CreateItems();
        }

        private void OnDisable()
        {
            _adapter.OnItemAdded -= OnItemAdded;
            _adapter.OnItemRemoved -= OnItemRemoved;
            _reorganizeButton.onClick.RemoveListener(_adapter.Reorganize);
        }

        private void CreateGrid()
        {
            for (int y = 0; y < _adapter.Height; y++)
            {
                for (int x = 0; x < _adapter.Width; x++)
                {
                    var cell = _container.InstantiatePrefab(_adapter.CellPrefab, _adapter.GridContainer);
                    var rect = cell.GetComponent<RectTransform>();
                    rect.sizeDelta = _adapter.CellSize;
                    rect.anchoredPosition = new Vector2(x * _adapter.CellSize.x, -y * _adapter.CellSize.y);
                    _cells[x, y] = cell.GetComponent<Cell>();
                }
            }
        }

        private void CreateItems()
        {
            foreach ((string id, Item item) in _adapter.Items)
            {
                Vector2Int[] itemPosition = _adapter.GetItemPosition(id);
                OnItemAdded(item, itemPosition);
            }
        }

        private void OnItemAdded(Item item, Vector2Int[] positions)
        {
            foreach (Vector2Int position in positions)
                _cells[position.x, position.y].SetItem(item);

            var instance = _container.InstantiatePrefab(_adapter.InventoryItemPrefab, _adapter.GridContainer);
            var itemRect = instance.GetComponent<RectTransform>();

            var cellRect = _cells[positions[0].x, positions[0].y].GetComponent<RectTransform>();

            itemRect.sizeDelta = new Vector2(
                item.itemData.Size.x * _adapter.CellSize.x, item.itemData.Size.y * _adapter.CellSize.y);

            itemRect.anchoredPosition = cellRect.anchoredPosition;
        }

        private void OnItemRemoved(Vector2Int position)
        {
            Debug.Log($"Item removed at {position}");
            if (_items.ContainsKey(position))
            {
                Destroy(_items[position].gameObject);
                _items.Remove(position);
            }
        }
    }
}

// Vector2Int position = _adapter.GetItemPosition(id);
// RectTransform cellRect = _cells[position.x, position.y].GetComponent<RectTransform>();
//
// GameObject itemObject = _container.InstantiatePrefab(_adapter.InventoryItemPrefab, _adapter.GridContainer);
// RectTransform itemRect = itemObject.GetComponent<RectTransform>();
//
// itemRect.sizeDelta = new Vector2(
//     item.itemData.Size.x * _adapter.CellSize.x,
//     item.itemData.Size.y * _adapter.CellSize.y);
// itemRect.anchoredPosition = cellRect.anchoredPosition;
//
// itemObject.GetComponent<InventoryItemView>().SetIcon(item.itemData.Icon);
// itemObject.GetComponent<DraggableItem>().SetItem(item);
//
// _items[position] = itemObject.GetComponent<InventoryView>();