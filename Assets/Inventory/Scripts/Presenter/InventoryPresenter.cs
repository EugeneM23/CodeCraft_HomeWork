using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Inventories
{
    public class InventoryPresenter
    {
        private readonly InventoryView _view;
        private readonly Inventory _inventory;
        private Vector2Int[] _highlightedCells;
        public Inventory Inventory => _inventory;

        public InventoryPresenter(InventoryView view, Inventory inventory)
        {
            _view = view;
            _inventory = inventory;
        }

        public void OnShow()
        {
            _highlightedCells = new Vector2Int[4];
            _view.InitializeGrid(Inventory.Width, Inventory.Height, Inventory);
            UpdateView();

            Inventory.OnAdded += OnItemAdded;
            Inventory.OnRemoved += OnItemRemoved;
            Inventory.OnHighlight += Highlight;
            Inventory.OnUnHighlight += UnHighlight;
            Inventory.OnCleared += OnCleared;
            _view.OnReorganize += Reorganize;
        }

        public void OnHide()
        {
            Inventory.OnAdded -= OnItemAdded;
            Inventory.OnRemoved -= OnItemRemoved;
            Inventory.OnHighlight -= Highlight;
            Inventory.OnUnHighlight -= UnHighlight;
            Inventory.OnCleared -= OnCleared;

            _view.OnReorganize -= Reorganize;
        }

        private void OnCleared()
        {
            _view.Clear();
            UpdateView();
        }

        private void UpdateView()
        {
            foreach (ItemInstance item in Inventory)
            {
                Vector2Int[] itemGridPositions = Inventory.GetItemGridPositions(item);
                _view.AddItem(item, itemGridPositions);
            }
        }

        private void OnItemAdded(ItemInstance itemInstance)
        {
            Vector2Int[] positions = Inventory.GetItemGridPositions(itemInstance);
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
                if (!IsValidCell(cellIndex) || !Inventory.IsFree(cellIndex))
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
            return cellIndex.x >= 0 && cellIndex.x < Inventory.Width &&
                   cellIndex.y >= 0 && cellIndex.y < Inventory.Height;
        }

        [Button]
        public void Reorganize()
        {
            Inventory.Reorganize();
        }
    }
}