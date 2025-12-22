using System;
using Sirenix.OdinInspector;
using UnityEngine;

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

        private InventoryHighlight _inventoryHighlight;
        private InventoryAudioController _audioController;

        public Inventory Inventory { get; private set; }
        public InventoryPresenter Presenter { get; private set; }

        public void Initialize(InventoryFactory factory, DragFSM dragFsm)
        {
            // Inventory
            Inventory = new Inventory(_columns, _rows);
            
            foreach (SceneItem item in _initializeItems)
                Inventory.AddItem(item.ItemData, item.Quantity);
            
            Presenter = new InventoryPresenter(_view, Inventory);

            _view.Initialize(Inventory.Width, Inventory.Height, Inventory, factory);
            Presenter.UpdateView();

            // Highlight
            _inventoryHighlight = new InventoryHighlight(dragFsm, _view, Inventory);

            // Add init items


            // Audio 
            _audioController = new InventoryAudioController(Inventory);
        }

        private void Update()
        {
            _inventoryHighlight?.Tick();
        }
    }
}