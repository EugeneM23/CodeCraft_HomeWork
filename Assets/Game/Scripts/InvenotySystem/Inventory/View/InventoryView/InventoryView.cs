using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

namespace Inventories
{
    public partial class InventoryView : MonoBehaviour
    {
        [SerializeField] private CellView cellViewPrefab;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;
        [SerializeField] private Vector2Int _cellSize;

        public Vector2Int CellSize => _cellSize;

        private Dictionary<string, GameObject> _items;
        private CellView[,] _cells;
        private InventoryPresenter _presenter;
        private DiContainer _container;

        [Inject]
        public void Construct(InventoryPresenter presenter, DiContainer container)
        {
            _presenter = presenter;
            _container = container;
            _cells = new CellView[presenter.Width, presenter.Height];
            _items = new Dictionary<string, GameObject>();
        }

        private void OnEnable()
        {
            _presenter.OnItemAdded += CreateItem;
            _presenter.OnItemRemoved += RemoveItem;
            _presenter.OnReorganize += Reorganize;

            SubscribeButtons();
        }

        private void OnDisable()
        {
            _presenter.OnItemAdded -= CreateItem;
            _presenter.OnItemRemoved -= RemoveItem;
            _presenter.OnReorganize -= Reorganize;

            UnsubscribeButtons();
        }

        private void Start()
        {
            CreateGrid();
            CreateAllItems();
        }

        public CellView[,] GetCells() => (CellView[,])_cells.Clone();

        public IReadOnlyDictionary<string, GameObject> GetItems() => _items;
    }
}