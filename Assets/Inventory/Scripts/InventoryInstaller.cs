using System;
using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Inventories
{
    public class InventoryInstaller : SerializedMonoBehaviour
    {
        [SerializeField] private InventoryView _view;
        [SerializeField] private SceneItem[] _initializeItems;
        [SerializeField] private int _columns = 4;
        [SerializeField] private int _rows = 7;
        [SerializeField] private IItemConsumer _itemConsumer;

        private InventoryPresenter _presenter;

        public Inventory Inventory { get; private set; }

        private void Awake()
        {
            Inventory = new Inventory(_columns, _rows);
            _presenter = new InventoryPresenter(_view, Inventory);

            Inventory.Owner = _itemConsumer;
            _itemConsumer.Inventory = Inventory;

            foreach (SceneItem item in _initializeItems)
                Inventory.AddItem(item.ItemData, item.Quantity);
        }

        private void OnEnable() => _presenter.OnShow();

        private void OnDisable() => _presenter.OnHide();
    }
}