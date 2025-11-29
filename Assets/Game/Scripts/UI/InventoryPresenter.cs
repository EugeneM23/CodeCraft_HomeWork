using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

public class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private GridToMatrix _gridToMatrix;
    private Inventory _inventory;

    private void Start()
    {
        CellView[,] cellViews = _gridToMatrix.matrix;
        _inventory = new Inventory(cellViews.GetLength(0), cellViews.GetLength(1));

        _inventory.AddItem(new Item("x", 3, 3));
        _inventory.AddItem(new Item("y", 2, 2));
        _inventory.AddItem(new Item("z", 2, 2));

        RenderState();
        _view.OnMoveItem += MoveItem;
    }

    private void MoveItem(Vector2Int position, Item item)
    {
        if (_inventory.MoveItem(item, position))
            _view._selectedItem = null;

        RenderState();
    }

    private void RenderState()
    {
        _view.Clear();
        foreach (Item item in _inventory)
            _view.AddItem(item, _inventory.GetPositions(item));
    }

    [Button]
    public void ReorganizeSpase()
    {
        _inventory.ReorganizeSpace();
        RenderState();
    }
}