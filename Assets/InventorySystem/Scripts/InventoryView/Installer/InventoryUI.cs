using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private RectTransform _inventoryItem;
        [SerializeField] private CellView _cellPrefab;
        [SerializeField] private Vector2Int _cellSize = new(64, 64);
        [SerializeField] private RectTransform _gridContainer;
        [SerializeField] private Button _reorganizeButton;
        [SerializeField] private Button _closeButton;

        [ShowInInspector] [Inject] private readonly InventoryAdapter _adapter;

        private CellView[,] _cells;

        private Dictionary<Vector2Int, InventoryItem> Items = new();

        private void Start()
        {
            _cells = new CellView[_adapter.Widht, _adapter.Height];


            CreateGrid(_adapter.Widht, _adapter.Height);
            CreateItem();
        }

        public void Show()
        {
            this.gameObject.SetActive(true);
        }

        public void Hide() => this.gameObject.SetActive(false);

        private void OnEnable()
        {
            _adapter.OnItemRemoved += OnIteRemoved;
            _adapter.OnStateChanged += OnStateChanged;
            _reorganizeButton.onClick.AddListener(Reorganize);
            _closeButton.onClick.AddListener(Hide);
        }

        private void OnDisable()
        {
            _adapter.OnItemRemoved -= OnIteRemoved;
            _adapter.OnStateChanged -= OnStateChanged;
            _reorganizeButton.onClick.RemoveListener(Reorganize);
            _closeButton.onClick.RemoveListener(Hide);
        }

        private void CreateItem()
        {
            foreach ((string id, Item item) in _adapter.Items)
            {
                RectTransform inventoryItem = Instantiate(_inventoryItem, _gridContainer);
                Vector2Int size = item.itemData.Size;

                inventoryItem.sizeDelta = new Vector2(size.x * _cellSize.x, size.y * _cellSize.y);


                Vector2Int itemPosition = _adapter.GetItemPosition(id);
                var rectTransform = _cells[itemPosition.x, itemPosition.y].GetComponent<RectTransform>();

                inventoryItem.anchoredPosition =
                    new Vector2(rectTransform.anchoredPosition.x, rectTransform.anchoredPosition.y);

                inventoryItem.GetComponent<InventoryItem>().SetIcon(item.itemData.Icon);

                Items[itemPosition] = inventoryItem.GetComponent<InventoryItem>();
            }
        }

        private void OnIteRemoved(Vector2Int position)
        {
            Destroy(Items[position].gameObject);
            Items.Remove(position);
        }

        private void Reorganize()
        {
            _adapter.Reorganize();
        }

        private void OnStateChanged()
        {
            foreach (var item in Items)
                Destroy(item.Value.gameObject);

            Items.Clear();


            CreateItem();
        }

        private void CreateGrid(int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    CreateCell(x, y);
            }
        }

        private void CreateCell(int x, int y)
        {
            var cell = Instantiate(_cellPrefab, this._gridContainer);
            RectTransform rect = cell.GetComponent<RectTransform>();
            rect.sizeDelta = _cellSize;
            rect.anchoredPosition = new Vector2(x * _cellSize.x, -y * _cellSize.y);

            _cells[x, y] = cell;
        }
    }
}