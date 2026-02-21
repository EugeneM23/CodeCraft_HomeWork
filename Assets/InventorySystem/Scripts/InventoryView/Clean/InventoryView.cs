using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zenject;

namespace Inventories
{
    public partial class InventoryView : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [FormerlySerializedAs("itemCellPrefab")] [FormerlySerializedAs("_cellPrefab")] [SerializeField] private InventoryCell inventoryCellPrefab;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;
        [SerializeField] private Image _draggableImage;
        [SerializeField] private Transform _gridContainer;
        [SerializeField] private Image _selectedArea;
        [SerializeField] private Vector2Int _cellSize;
        
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _reorganizeButton;
        [SerializeField] private Button _openEquipment;

        private Dictionary<string, GameObject> _items;
        private InventoryCell[,] _cells;

        private DragContext _dragContext;
        private InventoryPresenter _presenter;
        private DiContainer _container;

        public InventoryPresenter Presenter => _presenter;
        public Vector2Int CellSize => _cellSize;
        public Transform GridContainer => _gridContainer;
        public Image SelectedArea => _selectedArea;

        [Inject]
        public void Construct(InventoryPresenter presenter, DiContainer container, DragContext dragContext)
        {
            _presenter = presenter;
            _container = container;
            _dragContext = dragContext;
            _cells = new InventoryCell[presenter.Width, presenter.Height];
            _items = new Dictionary<string, GameObject>();
        }

        private void OnEnable()
        {
            _presenter.OnItemAdded += CreateItem;
            _presenter.OnItemRemoved += RemoveItem;
            _presenter.OnReorganize += Reorganize;

            _reorganizeButton.onClick.AddListener(() => _presenter.Reorganize());
            _closeButton.onClick.AddListener(() => this.gameObject.SetActive(false));
            _openEquipment.onClick.AddListener(() => _presenter.OpenEquipment());
        }

        private void OnDisable()
        {
            _presenter.OnItemAdded -= CreateItem;
            _presenter.OnItemRemoved -= RemoveItem;
            _presenter.OnReorganize -= Reorganize;
            
            _reorganizeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.RemoveAllListeners();
            _openEquipment.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            InitializeDragProcessor();
            CreateGrid();
            DisplayItems();
        }
        
        public InventoryCell[,] GetCells() => (InventoryCell[,])_cells.Clone();
       
    }
}