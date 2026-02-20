using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public partial class InventoryView : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private Cell _cellPrefab;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;
        [SerializeField] private Image _draggableImage;
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private Image _selectedArea;
        [SerializeField] private Vector2Int _cellSize;

        private Dictionary<string, GameObject> _items;
        private Cell[,] _cells;

        private DragContext _dragContext;
        private InventoryAdapterN _adapter;
        private DiContainer _container;

        public Cell[,] Cells => _cells;
        public InventoryAdapterN Adapter => _adapter;
        public Vector2Int CellSize => _cellSize;
        public Transform GridContainer => _gridContainer;
        public Image SelectedArea => _selectedArea;

        [Inject]
        public void Construct(InventoryAdapterN adapter, DiContainer container, DragContext dragContext)
        {
            _adapter = adapter;
            _container = container;
            _dragContext = dragContext;
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
    }
}