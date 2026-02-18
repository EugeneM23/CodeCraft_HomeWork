using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public class InventoryUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Inject] private InventoryAdapter _adapter;
        [Inject] private DiContainer _container;

        private Cell[,] _cells;
        private Dictionary<string, GameObject> _itemViews = new();

        private Item _draggedItem;
        private GameObject _draggedView;

        private void OnEnable()
        {
            _adapter.OnItemAdded += OnItemAdded;
            _adapter.OnItemRemoved += OnItemRemoved;
        }

        private void OnDisable()
        {
            _adapter.OnItemAdded -= OnItemAdded;
            _adapter.OnItemRemoved -= OnItemRemoved;
        }

        private void Start()
        {
            _cells = new Cell[_adapter.Width, _adapter.Height];
            CreateGrid();
            CreateItems();
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
                    _cells[x, y].Construct(new Vector2Int(x, y));
                }
            }
        }

        private void CreateItems()
        {
            foreach ((string id, Item item) in _adapter.Items)
                OnItemAdded(item, _adapter.GetItemPositions(id));
        }

        private void OnItemAdded(Item item, Vector2Int[] positions)
        {
            foreach (var pos in positions)
                _cells[pos.x, pos.y].SetItem(item);

            var instance = _container.InstantiatePrefab(_adapter.InventoryItemPrefab, _adapter.GridContainer);
            var itemRect = instance.GetComponent<RectTransform>();

            itemRect.sizeDelta = new Vector2(
                item.itemData.Size.x * _adapter.CellSize.x,
                item.itemData.Size.y * _adapter.CellSize.y);

            itemRect.anchoredPosition = _cells[positions[0].x, positions[0].y]
                .GetComponent<RectTransform>().anchoredPosition;

            _itemViews[item.ID] = instance;
        }

        private void OnItemRemoved(Item item, Vector2Int[] positions)
        {
            Destroy(_itemViews[item.ID]);
            _itemViews.Remove(item.ID);

            foreach (var pos in positions)
                _cells[pos.x, pos.y].SetItem(null);
        }

        // --- Drag & Drop ---

        public void OnBeginDrag(PointerEventData eventData)
        {
            Cell cell = eventData.pointerPressRaycast.gameObject.GetComponent<Cell>();
            if (cell == null || cell.Item == null) return;

            _draggedItem = cell.Item;
            _draggedView = _itemViews[_draggedItem.ID];

            _adapter.RemoveItem(_draggedItem.ID);

            // Поднимаем вьюшку поверх всего во время драга
            _draggedView.transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_draggedItem == null) return;

            Cell targetCell = eventData.pointerEnter?.GetComponent<Cell>();

            if (targetCell != null)
                targetCell.Adapter.AddItem(_draggedItem, targetCell.MatrixPosition);


            _draggedItem = null;
            _draggedView = null;
        }
    }
}