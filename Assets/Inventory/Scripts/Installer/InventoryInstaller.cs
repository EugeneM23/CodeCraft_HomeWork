using System;
using System.Collections.Generic;
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

        [SerializeField] private Equipment _equipment;

        [Header("Inventory Settings")] [SerializeField]
        private int _columns = 4;

        [SerializeField] private int _rows = 7;
        [SerializeField] private SceneItem[] _initializeItems;

        [Header("Dependencies")] [SerializeField]
        private DragItem _dragItemPrefab;

        [SerializeField] private List<SceneItem> _sceneItemCatalog;
        [SerializeField] private bool _canDrag = true;

        private InventoryPresenter _presenter;
        private RaycastDetector _raycastDetector;
        private DragFSM _dragFsm;
        private InventoryHighlight _inventoryHighlight;
        private InventoryFactory _factory;

        public InventoryPresenter Presenter => _presenter;

        public Inventory Inventory { get; private set; }

        public void Initialize(IItemConsumer consumer)
        {
            PrefabPool pool = new();

            _factory = new InventoryFactory(pool, _dragItemPrefab, _view.CellSize, Inventory, _view.transform);

            //Inventory
            Inventory = new Inventory(_columns, _rows);
            _presenter = new InventoryPresenter(_view, Inventory);
            _view.Initialize(Inventory.Width, Inventory.Height, Inventory, _factory);
            _presenter.UpdateView();
            _presenter.Show();

            //Consumer
            if (consumer != null)
            {
                Inventory.Owner = consumer;
                consumer.Inventory = Inventory;
                consumer.Equipment = _equipment;
            }

            //Raycast
            GraphicRaycaster raycaster = FindObjectOfType<GraphicRaycaster>();
            EventSystem eventSystem = EventSystem.current;

            _raycastDetector = new RaycastDetector(raycaster, eventSystem);

            //Drag

            _factory.Initialize(_sceneItemCatalog);

            if (_canDrag)
            {
                _dragFsm = new DragFSM(_raycastDetector, Inventory, _factory);
                _dragFsm.Initialize();
                _inventoryHighlight = new InventoryHighlight(_dragFsm, _view);
            }


            //Add init items
            foreach (SceneItem item in _initializeItems)
                Inventory.AddItem(item.ItemData, item.Quantity);
        }

        private void Update()
        {
            _dragFsm?.Tick();
            _inventoryHighlight?.Tick();
        }
    }
}