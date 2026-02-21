using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zenject;

namespace Inventories
{
    public partial class InventoryView : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
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
            DisplayItems();
        }

        public InventoryCell[,] GetCells() => (InventoryCell[,])_cells.Clone();

        public IReadOnlyDictionary<string, GameObject> GetItems() => _items;
    }
}