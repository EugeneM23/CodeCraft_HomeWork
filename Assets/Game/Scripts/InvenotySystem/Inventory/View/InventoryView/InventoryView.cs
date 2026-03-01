using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

namespace Inventories
{
    public partial class InventoryView : MonoBehaviour
    {
        public event Action OnShow;
        public event Action OnHide;

        [SerializeField] private CellView cellViewPrefab;
        [SerializeField] private InventoryItemView _inventoryItemPrefab;
        [SerializeField] private Vector2Int _cellSize;

        public int ID => gameObject.GetInstanceID();

        private Dictionary<string, GameObject> _items;
        private CellView[,] _cells;
        private InventoryPresenter _presenter;
        private DiContainer _container;
        private SignalBus _signalBuss;

        [Inject]
        public void Construct(InventoryPresenter presenter, DiContainer container, SignalBus signalBus)
        {
            _presenter = presenter;
            _container = container;
            _signalBuss = signalBus;

            _cells = new CellView[presenter.Width, presenter.Height];
            _items = new Dictionary<string, GameObject>();
        }

        private void OnEnable()
        {
            _presenter.OnItemAdded += CreateItem;
            _presenter.OnItemRemoved += RemoveItem;
            _presenter.OnReorganize += Reorganize;

            SubscribeButtons();

            _isEquipmentEnabled = true;

            OnShow?.Invoke();
        }

        private void OnDisable()
        {
            _presenter.OnItemAdded -= CreateItem;
            _presenter.OnItemRemoved -= RemoveItem;
            _presenter.OnReorganize -= Reorganize;

            UnsubscribeButtons();

            OnHide?.Invoke();
        }

        private void Start()
        {
            CreateGrid();
            CreateAllItems();
        }

        public void SetEquipmentID(int equipmentId) => _equipmentID = equipmentId;
        
    }
}