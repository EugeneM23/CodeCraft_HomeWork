using Game.Scripts.UI.Equipment.Game.Equipment;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Inventories
{
    public class InventoryBootstrap : SerializedMonoBehaviour
    {
        [SerializeField] private InventoryView _view;
        [SerializeField] private int _columns = 4;
        [SerializeField] private int _rows = 7;
        [SerializeField] private SceneItem[] _initializeItems;
        [SerializeField] private EquipmentBootstrap _equipmentBootstrap;
        
        private InventoryHighlight _inventoryHighlight;
        private InventoryAudioController _audioController;

        public InventoryPresenter Presenter { get; private set; }

        public void Construct(InventoryFactory factory, DragFSM dragFsm, ItemConsumer consumer = null)
        {
            Inventory inventory = new Inventory(_columns, _rows);

            Presenter = new InventoryPresenter(_view, inventory, factory);

            AddInitialItems();

            _inventoryHighlight = new InventoryHighlight(dragFsm, _view, Presenter);
            _audioController = new InventoryAudioController(inventory);

            gameObject.SetActive(false);

            if (_equipmentBootstrap != null)
            {
                _equipmentBootstrap.Initialize(consumer);
            }
        }

        private void AddInitialItems()
        {
            foreach (SceneItem item in _initializeItems)
            {
                Presenter.AddItem(item.ItemData, item.Quantity);
            }
        }

        private void Update()
        {
            _inventoryHighlight?.Tick();
        }
    }
}