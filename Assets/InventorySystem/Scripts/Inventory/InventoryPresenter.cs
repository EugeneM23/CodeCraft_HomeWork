using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inventories
{
    public class InventoryPresenter
    {
        public event Action OnShow;
        public event Action OnHide;
        private readonly InventoryView _view;
        private readonly Inventory _inventory;
        private InventoryPresenter _mainInventory;
        private bool _isOpen;
        public IItemConsumer Owner { get; set; }

        public InventoryPresenter(InventoryView view, Inventory inventory, InventoryFactory factory)
        {
            _view = view;
            _inventory = inventory;
            _view.Initialize(inventory.Width, inventory.Height, this, factory);
        }

        public void Show(InventoryPresenter presenter = null)
        {
            if (_isOpen)
            {
                _mainInventory = presenter;
                return;
            }

            _mainInventory = presenter;
            _isOpen = true;

            Subscribe();
            _view.SetActive(true);
            UpdateView();

            OnShow?.Invoke();
        }

        private void Hide()
        {
            if (!_isOpen) return;

            _isOpen = false;
            Unsubscribe();
            _view.SetActive(false);
            OnHide?.Invoke();
        }

        private void Subscribe()
        {
            _inventory.OnAdded += OnItemAdded;
            _inventory.OnRemoved += OnItemRemoved;
            _inventory.OnCleared += UpdateView;
            _view.OnReorganizeClicked += _inventory.Reorganize;
            _view.OnCollectAllClicked += OnCollectAll;
            _view.OnCloseClicked += Hide;
        }

        private void Unsubscribe()
        {
            _inventory.OnAdded -= OnItemAdded;
            _inventory.OnRemoved -= OnItemRemoved;
            _inventory.OnCleared -= UpdateView;
            _view.OnReorganizeClicked -= _inventory.Reorganize;
            _view.OnCollectAllClicked -= OnCollectAll;
            _view.OnCloseClicked -= Hide;
        }

        private void UpdateView()
        {
            _view.ClearAllItems();

            foreach (Item item in _inventory)
            {
                Vector2Int[] positions = _inventory.GetItemGridPositions(item);
                _view.DisplayItem(item, positions, this);
            }
        }

        private void OnItemAdded(Item item)
        {
            Vector2Int[] positions = _inventory.GetItemGridPositions(item);
            _view.DisplayItem(item, positions, this);
        }

        private void OnItemRemoved(Item item) => _view.RemoveItem(item.ID);

        private void OnCollectAll()
        {
            Debug.Log($"OnCollectAll called. MainInventory owner: {_mainInventory?.Owner}");

            var itemsToCollect = new List<Item>(_inventory);

            foreach (Item item in itemsToCollect)
            {
                if (_mainInventory.AddItem(item.itemData, item.StackQuantity))
                    _inventory.RemoveItem(item.ID);
            }
        }

        public void RemoveItem(string itemID)
        {
            _inventory.RemoveItem(itemID);
        }

        public bool AddItem(ItemData draggedItemItemData, Vector2Int targetPosition, int draggedItemStackQuantity)
        {
            return _inventory.AddItem(draggedItemItemData, targetPosition, draggedItemStackQuantity);
        }

        public Vector2Int GetItemGridPositions(Item item)
        {
            return _inventory.GetPositions(item.ID)[0];
        }

        public bool AddItem(ItemData draggedItemItemData, int sceneItemQuantity)
        {
            return _inventory.AddItem(draggedItemItemData, sceneItemQuantity);
        }

        public bool AddItem(ItemData currentItemItemData)
        {
            return _inventory.AddItem(currentItemItemData);
        }

        public void UpdateMainInventory(InventoryPresenter presenter)
        {
            _mainInventory = presenter;
        }

        public Inventory GetInventory() => _inventory;
    }
}