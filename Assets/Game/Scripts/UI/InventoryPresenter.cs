using UnityEngine;
using Inventories;
using Sirenix.OdinInspector;

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
        _view.OnItemDragged += TryMoveItem;
    }

    private void TryMoveItem(Item item, Vector2Int fromPosition, Vector2Int toPosition)
    {
        // Вычисляем смещение
        Vector2Int offset = toPosition - fromPosition;

        // Получаем текущие позиции предмета
        Vector2Int[] currentPositions = _inventory.GetPositions(item);

        // Проверяем, можно ли переместить (используем первую позицию как базовую)
        Vector2Int firstPos = currentPositions[0];
        Vector2Int newFirstPos = firstPos + offset;

        // Проверяем через модель
        if (_inventory.MoveItem(item, newFirstPos))
        {
            RenderState();
        }
        else
        {
            // Не удалось переместить - перерисовываем чтобы вернуть на место
            RenderState();
        }
    }

    private void RenderState()
    {
        _view.Clear();
        foreach (Item item in _inventory)
        {
            _view.DisplayItem(item, _inventory.GetPositions(item));
        }
    }

    [Button]
    public void ReorganizeSpace()
    {
        _inventory.ReorganizeSpace();
        RenderState();
    }
}