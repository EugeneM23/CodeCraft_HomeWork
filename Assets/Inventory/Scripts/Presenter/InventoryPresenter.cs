using System.Collections.Generic;
using UnityEngine;

namespace Inventories
{
    public class InventoryPresenter
    {
        private readonly InventoryView _view;
        private readonly Inventory _inventory;
        private Vector2Int[] _highlightedCells = new Vector2Int[0];
        private Inventory _mainInventory;

        private bool _isOpen;

        public InventoryPresenter(InventoryView view, Inventory inventory, InventoryFactory factory)
        {
            _view = view;
            _inventory = inventory;

            _view.Initialize(inventory.Width, inventory.Height, inventory, factory);
        }

        public void Subscribe()
        {
            _inventory.OnAdded += OnItemAdded;
            _inventory.OnRemoved += OnItemRemoved;
            _inventory.OnHighlight += OnHighlight;
            _inventory.OnUnHighlight += ClearHighlight;
            _inventory.OnCleared += UpdateView;
            _view.OnReorganizeClicked += OnReorganize;
            _view.OnCollectAllClicked += OnCollectAll;
            _view.OnCloseClicked += OnHide;
        }

        private void Unsubscribe()
        {
            _inventory.OnAdded -= OnItemAdded;
            _inventory.OnRemoved -= OnItemRemoved;
            _inventory.OnHighlight -= OnHighlight;
            _inventory.OnUnHighlight -= ClearHighlight;
            _inventory.OnCleared -= UpdateView;
            _view.OnReorganizeClicked -= OnReorganize;
            _view.OnCollectAllClicked -= OnCollectAll;
            _view.OnCloseClicked -= OnHide;
        }

        private void OnShow(Inventory mainInventory)
        {
            if (_isOpen) return;

            _mainInventory = mainInventory;
            _isOpen = true;

            Subscribe();
            _view.SetActive(true);
            UpdateView();
        }

        private void OnHide()
        {
            if (!_isOpen) return;

            _isOpen = false;
            Unsubscribe();
            _view.SetActive(false);
        }

        public void Toggle(Inventory mainInventory)
        {
            if (_isOpen)
                OnHide();
            else
                OnShow(mainInventory);
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

        private void OnItemRemoved(ItemInstance item)
        {
            _view.RemoveItem(item.ID);
        }

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

            _highlightedCells = new Vector2Int[0];
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

        private bool IsValidCell(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < _inventory.Width &&
                   cell.y >= 0 && cell.y < _inventory.Height;
        }

        private void OnReorganize()
        {
            _inventory.Reorganize();
        }

        private void OnCollectAll()
        {
            var itemsToCollect = new List<ItemInstance>(_inventory);

            foreach (ItemInstance item in itemsToCollect)
            {
                if (_mainInventory.AddItem(item.itemData, item.StackQuantity))
                    _inventory.RemoveItem(item.ID);
            }
        }
    }
}