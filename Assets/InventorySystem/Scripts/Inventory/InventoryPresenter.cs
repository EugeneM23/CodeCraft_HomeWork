using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inventories
{
    public class InventoryPresenter
    {
        private readonly InventoryView _view;
        private readonly Inventory _inventory;
        private Vector2Int[] _highlightedCells = Array.Empty<Vector2Int>();
        private Inventory _mainInventory;
        private EquipmentPresenter _equipment;
        private bool _isOpen;

        public InventoryPresenter(InventoryView view, Inventory inventory, InventoryFactory factory)
        {
            _view = view;
            _inventory = inventory;
            _view.Initialize(inventory.Width, inventory.Height, inventory, factory);
        }

        private void Subscribe()
        {
            _inventory.OnAdded += OnItemAdded;
            _inventory.OnRemoved += OnItemRemoved;
            _inventory.OnHighlight += OnHighlight;
            _inventory.OnUnHighlight += ClearHighlight;
            _inventory.OnCleared += UpdateView;
            _view.OnReorganizeClicked += _inventory.Reorganize;
            _view.OnCollectAllClicked += OnCollectAll;
            _view.OnCloseClicked += Hide;

            if (_equipment != null)
                _view.OnEquipClicked += _equipment.Toggle;
        }

        private void Unsubscribe()
        {
            _inventory.OnAdded -= OnItemAdded;
            _inventory.OnRemoved -= OnItemRemoved;
            _inventory.OnHighlight -= OnHighlight;
            _inventory.OnUnHighlight -= ClearHighlight;
            _inventory.OnCleared -= UpdateView;
            _view.OnReorganizeClicked -= _inventory.Reorganize;
            _view.OnCollectAllClicked -= OnCollectAll;
            _view.OnCloseClicked -= Hide;

            if (_equipment != null)
                _view.OnEquipClicked -= _equipment.Toggle;
        }

        private void Show(Inventory mainInventory)
        {
            if (_isOpen) return;

            _mainInventory = mainInventory;
            _isOpen = true;

            Subscribe();
            _view.SetActive(true);
            UpdateView();
            //_equipment?.Show();
        }

        private void Hide()
        {
            if (!_isOpen) return;

            _isOpen = false;
            Unsubscribe();
            _view.SetActive(false);
        }

        public void Toggle(Inventory mainInventory)
        {
            if (_isOpen)
                Hide();
            else
                Show(mainInventory);
        }

        private void UpdateView()
        {
            _view.ClearAllItems();

            foreach (ItemInstance item in _inventory)
            {
                Vector2Int[] positions = _inventory.GetItemGridPositions(item);
                _view.DisplayItem(item, positions);
            }
        }

        private void OnItemAdded(ItemInstance item)
        {
            Vector2Int[] positions = _inventory.GetItemGridPositions(item);
            _view.DisplayItem(item, positions);
        }

        private void OnItemRemoved(ItemInstance item) => _view.RemoveItem(item.ID);

        private void OnHighlight(Vector2Int[] cells)
        {
            ClearHighlight();

            if (!CanHighlightCells(cells)) return;

            _highlightedCells = cells;

            foreach (Vector2Int cell in cells)
                _view.HighlightCell(cell);
        }

        private void ClearHighlight()
        {
            foreach (Vector2Int cell in _highlightedCells)
            {
                if (IsValidCell(cell))
                    _view.UnhighlightCell(cell);
            }

            _highlightedCells = Array.Empty<Vector2Int>();
        }

        private bool CanHighlightCells(Vector2Int[] cells)
        {
            foreach (Vector2Int cell in cells)
            {
                if (!IsValidCell(cell) || !_inventory.IsFree(cell))
                    return false;
            }

            return true;
        }

        private bool IsValidCell(Vector2Int cell) =>
            cell.x >= 0 && cell.x < _inventory.Width &&
            cell.y >= 0 && cell.y < _inventory.Height;

        private void OnCollectAll()
        {
            var itemsToCollect = new List<ItemInstance>(_inventory);

            foreach (ItemInstance item in itemsToCollect)
            {
                if (_mainInventory.AddItem(item.itemData, item.StackQuantity))
                    _inventory.RemoveItem(item.ID);
            }
        }

        public void SetEquipment(EquipmentPresenter equipment) => _equipment = equipment;
    }
}