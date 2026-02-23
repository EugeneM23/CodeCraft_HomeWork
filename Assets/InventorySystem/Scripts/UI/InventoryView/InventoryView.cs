using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Inventories
{
    public partial class InventoryView : MonoBehaviour
    {
        public event Action OnInventoryOpened;
        public event Action OnInventoryClosed;

        [SerializeField] private InventoryCell inventoryCellPrefab;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;
        [SerializeField] private Vector2Int _cellSize;

        public Vector2Int CellSize => _cellSize;

        private Dictionary<string, GameObject> _items;
        private InventoryCell[,] _cells;
        private InventoryPresenter _presenter;
        private DiContainer _container;

        [Inject]
        public void Construct(InventoryPresenter presenter, DiContainer container)
        {
            _presenter = presenter;
            _container = container;
            _cells = new InventoryCell[presenter.Width, presenter.Height];
            _items = new Dictionary<string, GameObject>();
        }

        private void OnEnable()
        {
            _presenter.OnItemAdded += CreateItem;
            _presenter.OnItemRemoved += RemoveItem;
            _presenter.OnReorganize += Reorganize;

            SubscribeButtons();

            OnInventoryOpened?.Invoke();
        }

        private void OnDisable()
        {
            _presenter.OnItemAdded -= CreateItem;
            _presenter.OnItemRemoved -= RemoveItem;
            _presenter.OnReorganize -= Reorganize;

            UnsubscribeButtons();

            OnInventoryClosed?.Invoke();
        }

        private void Start()
        {
            CreateGrid();
            CreateAllItems();
        }

        public InventoryCell[,] GetCells() => (InventoryCell[,])_cells.Clone();

        public IReadOnlyDictionary<string, GameObject> GetItems() => _items;
    }
}