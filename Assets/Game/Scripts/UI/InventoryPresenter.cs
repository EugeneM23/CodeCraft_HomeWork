using Inventories;
using Sirenix.OdinInspector;
using UnityEngine;

using UnityEngine;
using Inventories;

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
        _view.OnItemMoved += OnItemMoved;
    }

    private void OnItemMoved(Item item, Vector2Int newPosition)
    {
        // View уже проверил и переместил визуально
        // Теперь синхронизируем модель
        _inventory.MoveItem(item, newPosition);
    }

    private void RenderState()
    {
        _view.Clear();
        foreach (Item item in _inventory)
        {
            _view.AddItem(item, _inventory.GetPositions(item));
        }
    }

    [Button]
    public void ReorganizeSpace()
    {
        _inventory.ReorganizeSpace();
        RenderState();
    }
}