using System.Collections.Generic;
using UnityEngine;

namespace Inventories
{
    public class InventoryPresenter
    {
        private readonly InventoryView _view;
        private readonly Inventory _inventory;
        private Vector2Int[] _highlightedCells;

        private Inventory _mainInventory;
        public bool IsOpen { get; private set; }

        public InventoryPresenter(InventoryView view, Inventory inventory)
        {
            _view = view;
            _inventory = inventory;
        }

        public void Show(Inventory mainInventory)
        {
            _mainInventory = mainInventory;
            IsOpen = true;
            _highlightedCells = new Vector2Int[4];
            _view.gameObject.SetActive(true);
            _inventory.OnAdded += OnItemAdded;
            _inventory.OnRemoved += OnItemRemoved;
            _inventory.OnHighlight += Highlight;
            _inventory.OnUnHighlight += UnHighlight;
            _inventory.OnCleared += UpdateView;
            _view.OnReorganize += Reorganize;
            _view.OnCollectAll += OnCollectAll;
            _view.OnClose += Onclose;

            UpdateView();
        }

        private void Onclose()
        {
            _view.gameObject.SetActive(false);
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

        public void Hide()
        {
            _view.gameObject.SetActive(false);
            IsOpen = false;
            _inventory.OnAdded -= OnItemAdded;
            _inventory.OnRemoved -= OnItemRemoved;
            _inventory.OnHighlight -= Highlight;
            _inventory.OnUnHighlight -= UnHighlight;
            _inventory.OnCleared -= UpdateView;
            _view.OnReorganize -= Reorganize;
            _view.OnCollectAll -= OnCollectAll;
            _view.OnClose -= Onclose;
        }

        public void UpdateView()
        {
            _view.Clear();

            foreach (ItemInstance item in _inventory)
            {
                Vector2Int[] itemGridPositions = _inventory.GetItemGridPositions(item);
                _view.AddItem(item, itemGridPositions);
            }
        }

        private void OnItemAdded(ItemInstance itemInstance)
        {
            Vector2Int[] positions = _inventory.GetItemGridPositions(itemInstance);
            _view.AddItem(itemInstance, positions);
        }

        private void OnItemRemoved(ItemInstance itemInstance)
        {
            _view.RemoveItem(itemInstance.ID);
        }

        private void Highlight(Vector2Int[] cells)
        {
            UnHighlight();
            _highlightedCells = cells;

            foreach (Vector2Int cellIndex in cells)
            {
                if (!IsValidCell(cellIndex) || !_inventory.IsFree(cellIndex))
                    return;
            }

            foreach (Vector2Int cellIndex in cells)
                _view.GetCell(cellIndex).Highlight(true);
        }

        private void UnHighlight()
        {
            foreach (Vector2Int cellIndex in _highlightedCells)
            {
                if (IsValidCell(cellIndex))
                    _view.GetCell(cellIndex).UnHighlight();
            }
        }

        private bool IsValidCell(Vector2Int cellIndex)
        {
            return cellIndex.x >= 0 && cellIndex.x < _inventory.Width &&
                   cellIndex.y >= 0 && cellIndex.y < _inventory.Height;
        }

        private void Reorganize()
        {
            _inventory.Reorganize();
        }

        public void SetMainInventory(Inventory inventory)
        {
            _mainInventory = inventory;
        }
    }
}