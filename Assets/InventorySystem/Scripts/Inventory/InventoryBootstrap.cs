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
        [SerializeField] private EquipmentPresenter _equipmentPresenter;
        [SerializeField] private InventoryAudioController _inventoryAudioController;
        [SerializeField] private EquipmentAudioController _equipmentAudioController;

        // private InventoryHighlight _inventoryHighlight;

        public InventoryPresenter Presenter { get; private set; }
        public EquipmentPresenter Equipment => _equipmentPresenter;

        public void Construct(InventoryFactory factory, 
            //DragFSM dragFSM,
            ItemConsumer consumer = null)
        {
            InitializeInventory(factory);
            AddInitialItems();
            //InitializeSystems(dragFSM);
            InitializeEquipment(consumer);
            LinkConsumer(consumer);

            gameObject.SetActive(false);
        }

        private void InitializeInventory(InventoryFactory factory)
        {
            Inventory inventory = new Inventory(_columns, _rows);
            Presenter = new InventoryPresenter(_view, inventory, factory);
        }

        private void AddInitialItems()
        {
            foreach (SceneItem item in _initializeItems)
            {
                Presenter.AddItem(item.ItemData, item.Quantity);
            }
        }

        private void InitializeSystems(
            //DragFSM dragFSM
            )
        {
            //_inventoryHighlight = new InventoryHighlight(dragFSM, _view, Presenter);

            if (_inventoryAudioController != null)
                _inventoryAudioController.Initialize(Presenter);
        }

        private void InitializeEquipment(ItemConsumer consumer)
        {
            if (_equipmentPresenter == null)
                return;

            _equipmentPresenter.Initialize(_view, Presenter);

            if (_equipmentAudioController != null)
                _equipmentAudioController.Initialize(_equipmentPresenter);

            if (consumer != null)
            {
                consumer.SetEquipment(_equipmentPresenter);
            }
        }

        private void LinkConsumer(ItemConsumer consumer)
        {
            if (consumer == null)
                return;

            Presenter.Owner = consumer;
            consumer.SetInventory(Presenter);
        }

        private void Update()
        {
            // _inventoryHighlight?.Tick();
        }
    }
}