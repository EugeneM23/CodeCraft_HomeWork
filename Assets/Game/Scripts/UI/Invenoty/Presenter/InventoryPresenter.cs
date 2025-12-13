using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class InventoryPresenter : MonoBehaviour, IInventoryCollection
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;
    private Inventory _inventory;
    private Vector2Int[] _highlightedCells;

    private void Awake()
    {
        _highlightedCells = new Vector2Int[_columns * _rows];

        _inventory = new Inventory(_columns, _rows);
        _view.InitializeGrid(_columns, _rows, this);
    }

    public bool AddItem(ItemData itemData, Vector2Int startPosition = default)
    {
        ItemInstance instance;

        if (startPosition == default)
        {
            instance = _inventory.AddItem(itemData);
        }
        else
        {
            instance = _inventory.AddItem(itemData, startPosition);
        }

        if (instance != null)
        {
            Vector2Int[] positions = _inventory.GetItemGridPositions(instance);
            _view.CreateInventoryItem(instance, positions);
            return true;
        }


        return false;
    }

    public Vector2Int GetItemPosition(ItemInstance instance)
        => instance.GridPosition;

    public void RemoveItem(string id)
    {
        _inventory.RemoveInstance(id);
        _view.RemoveItem(id);
    }

    [Button]
    public void Reorganize()
    {
        // _inventory.ReorganizeSpace();
        // UpdateView();
    }

    public void Highlight(Vector2Int[] cells)
    {
        UnHighlight();

        _highlightedCells = cells;

        foreach (Vector2Int cellIndex in cells)
            if (!IsValidCell(cellIndex) || !_inventory.IsFree(cellIndex))
                return;

        foreach (Vector2Int cellIndex in cells)
            _view.GetCell(cellIndex).Highlight(true);
    }

    public void UnHighlight()
    {
        foreach (Vector2Int cellIndex in _highlightedCells)
            if (IsValidCell(cellIndex))
                _view.GetCell(cellIndex).UnHighlight();
    }

    private bool IsValidCell(Vector2Int cellIndex)
    {
        return cellIndex.x >= 0 && cellIndex.x < _columns &&
               cellIndex.y >= 0 && cellIndex.y < _rows;
    }
}