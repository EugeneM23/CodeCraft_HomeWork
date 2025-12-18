using UnityEngine;

namespace Inventories
{
    public class InventoryPresenter
    {
        private readonly InventoryView _view;
        private readonly Inventory _inventory;
        private Vector2Int[] _highlightedCells;

        public bool IsOpen { get; private set; }

        public InventoryPresenter(InventoryView view, Inventory inventory)
        {
            _view = view;
            _inventory = inventory;
        }

        public void Show()
        {
            _highlightedCells = new Vector2Int[4];
            _view.gameObject.SetActive(true);
            IsOpen = true;
            _inventory.OnAdded += OnItemAdded;
            _inventory.OnRemoved += OnItemRemoved;
            _inventory.OnHighlight += Highlight;
            _inventory.OnUnHighlight += UnHighlight;
            _inventory.OnCleared += OnCleared;
            _view.OnReorganize += Reorganize;
        }

        public void Hide()
        {
            _view.gameObject.SetActive(false);
            IsOpen = false;
            _inventory.OnAdded -= OnItemAdded;
            _inventory.OnRemoved -= OnItemRemoved;
            _inventory.OnHighlight -= Highlight;
            _inventory.OnUnHighlight -= UnHighlight;
            _inventory.OnCleared -= OnCleared;

            _view.OnReorganize -= Reorganize;
        }

        private void OnCleared()
        {
            _view.Clear();
            UpdateView();
        }

        public void UpdateView()
        {
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
    }
}