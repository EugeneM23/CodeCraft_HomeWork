using System.Collections.Generic;
using Inventories;
using UnityEngine;

public partial class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private InventoryItemCatalog _itemCatalog;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;
    private Inventory _inventory;
    private readonly List<CellView> _highlightedCells = new();

    private void Awake()
    {
        _inventory = new Inventory(_columns, _rows);
        _view.InitializeGrid(_columns, _rows, this);

        UpdateView();
    }

    private bool CanAddItem(Item item, Vector2Int position) => _inventory.CanAddItem(item, position);
    public Vector2Int GetItemPosition(Item cellViewItem) => _inventory.GetPositions(cellViewItem)[0];
}