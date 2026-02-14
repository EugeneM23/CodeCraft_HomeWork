using System.Collections.Generic;
using System.Linq;
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

        private CellView[,] _cells;
        private Dictionary<Vector2Int, InventoryItem> _items = new();

        private void Start()
        {
            _cells = new CellView[_adapter.Width, _adapter.Height];
            CreateGrid();
            CreateItems();
        }

        private void OnEnable()
        {
            _adapter.OnItemRemoved += OnItemRemoved;
            _adapter.OnStateChanged += OnStateChanged;
            _reorganizeButton.onClick.AddListener(_adapter.Reorganize);
            _closeButton.onClick.AddListener(Hide);
        }

        private void OnDisable()
        {
            _adapter.OnItemRemoved -= OnItemRemoved;
            _adapter.OnStateChanged -= OnStateChanged;
            _reorganizeButton.onClick.RemoveListener(_adapter.Reorganize);
            _closeButton.onClick.RemoveListener(Hide);
        }

        public void Show() => gameObject.SetActive(true);

        public void Hide() => gameObject.SetActive(false);

        private void CreateGrid()
        {
            for (int y = 0; y < _adapter.Height; y++)
            {
                for (int x = 0; x < _adapter.Width; x++)
                {
                    var cell = Instantiate(_adapter.CellPrefab, _adapter.GridContainer);
                    var rect = cell.GetComponent<RectTransform>();

                    rect.sizeDelta = _adapter.CellSize;
                    rect.anchoredPosition = new Vector2(x * _adapter.CellSize.x, -y * _adapter.CellSize.y);

                    _cells[x, y] = cell;
                }
            }
        }

        private void CreateItems()
        {
            foreach (var (id, item) in _adapter.Items)
            {
                var itemObject =
                    _container.InstantiatePrefab(_adapter.InventoryItemPrefab, _adapter.GridContainer.transform);
                Vector2Int itemPosition = _adapter.GetItemPosition(id);
                RectTransform cellRect = _cells[itemPosition.x, itemPosition.y].GetComponent<RectTransform>();

                itemObject.GetComponent<RectTransform>().sizeDelta = new Vector2(
                    item.itemData.Size.x * _adapter.CellSize.x,
                    item.itemData.Size.y * _adapter.CellSize.y);

                itemObject.GetComponent<RectTransform>().anchoredPosition = cellRect.anchoredPosition;
                itemObject.GetComponent<InventoryItem>().SetIcon(item.itemData.Icon);
                itemObject.GetComponent<InventoryItem>().SetView(this);

                _items[itemPosition] = itemObject.GetComponent<InventoryItem>();
            }
        }

        private void OnItemRemoved(Vector2Int position)
        {
            Destroy(_items[position].gameObject);
            _items.Remove(position);
        }

        private void OnStateChanged()
        {
            foreach (var item in _items.Values)
                Destroy(item.gameObject);

            _items.Clear();
            CreateItems();
        }

        public void RemoveItem(InventoryItem inventoryItem)
        {
            var position = _items.FirstOrDefault(x => x.Value == inventoryItem).Key;
            _adapter.RemoveItem(position);
        }
    }
}