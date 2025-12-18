using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inventories
{
    public class InventoryInstaller : SerializedMonoBehaviour
    {
        [Header("View Settings")] [SerializeField]
        private InventoryView _view;

        [Header("Inventory Settings")] [SerializeField]
        private int _columns = 4;

        [SerializeField] private int _rows = 7;
        [SerializeField] private SceneItem[] _initializeItems;

        [Header("Dependencies")] [SerializeField]
        private IItemConsumer _itemConsumer;

        [SerializeField] private SceneItemSpawner _spawner;
        [SerializeField] private DragItem _dragItemPrefab;

        private InventoryPresenter _presenter;
        private RaycastDetector _raycastDetector;
        private DragFSM _dragFsm;
        private InventoryHighlight _inventoryHighlight;

        public Inventory Inventory { get; private set; }

        private void Awake()
        {
            InitializeInventory();
            ConfigureItemConsumer();
            PopulateInitialItems();
            InitializeRaycastSystem();
            InitializeDragSystem();
            InitializeHighlightSystem();
        }

        private void OnEnable()
        {
            _presenter.OnShow();
        }

        private void OnDisable()
        {
            _presenter.OnHide();
        }

        private void Update()
        {
            _dragFsm.Tick();
            _inventoryHighlight.Tick();
        }

        private void InitializeInventory()
        {
            Inventory = new Inventory(_columns, _rows);
            _presenter = new InventoryPresenter(_view, Inventory);
        }

        private void ConfigureItemConsumer()
        {
            Inventory.Owner = _itemConsumer;
            _itemConsumer.Inventory = Inventory;
        }

        private void PopulateInitialItems()
        {
            foreach (SceneItem item in _initializeItems)
                Inventory.AddItem(item.ItemData, item.Quantity);
        }

        private void InitializeRaycastSystem()
        {
            GraphicRaycaster raycaster = FindObjectOfType<GraphicRaycaster>();
            EventSystem eventSystem = EventSystem.current;

            _raycastDetector = new RaycastDetector(raycaster, eventSystem);
        }

        private void InitializeDragSystem()
        {
            _dragFsm = new DragFSM(_view, _raycastDetector, Inventory, _dragItemPrefab, _spawner);
            _dragFsm.Initialize();
        }

        private void InitializeHighlightSystem()
        {
            _inventoryHighlight = new InventoryHighlight(_dragFsm, _view);
        }
    }
}