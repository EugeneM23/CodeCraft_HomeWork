using Sirenix.OdinInspector;
using UnityEngine;

namespace Inventories
{
    public class InventoryInstaller : SerializedMonoBehaviour
    {
        [SerializeField] private InventoryView _view;
        [SerializeField] private int _columns = 4;
        [SerializeField] private int _rows = 7;
        [SerializeField] private SceneItem[] _initializeItems;

        private InventoryHighlight _inventoryHighlight;
        private InventoryAudioController _audioController;

        public Inventory Inventory { get; private set; }
        public InventoryBootstrap Bootstrap { get; private set; }

        public void Initialize(InventoryFactory factory, DragFSM dragFsm)
        {
            Inventory = new Inventory(_columns, _rows);
            
            AddInitialItems();
            
            Bootstrap = new InventoryBootstrap(_view, Inventory, factory);
            
            _inventoryHighlight = new InventoryHighlight(dragFsm, _view, Inventory);
            _audioController = new InventoryAudioController(Inventory);
            
            gameObject.SetActive(false);
        }

        private void AddInitialItems()
        {
            foreach (SceneItem item in _initializeItems)
            {
                Inventory.AddItem(item.ItemData, item.Quantity);
            }
        }

        private void Update()
        {
            _inventoryHighlight?.Tick();
        }
    }
}