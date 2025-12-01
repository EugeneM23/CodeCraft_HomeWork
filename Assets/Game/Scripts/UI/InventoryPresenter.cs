using UnityEngine;
using Inventories;
using Sirenix.OdinInspector;

public class InventoryPresenter : MonoBehaviour
{
    [SerializeField] private InventoryView _view;
    [SerializeField] private InventoryItemCatalog _itemCatalog;
    [SerializeField] private int _columns = 4;
    [SerializeField] private int _rows = 7;

    public Inventory _inventory;

    private void Start()
    {
        _inventory = new Inventory(_columns, _rows);
        _inventory.OnMoved += OnItemMoved;

        _view.InitializeGrid(_columns, _rows);

        AddTestItems();
        RenderAll();
    }

    private void OnItemMoved(Item item, Vector2Int newPosition)
    {
        RedrawItem(item);
    }

    private void RedrawItem(Item item)
    {
        Vector2Int[] positions = _inventory.GetPositions(item);
        
        // Очищаем старые клетки
        ClearItemCells(item);
        
        // Удаляем старый контейнер
        _view.RemoveItemContainer(item);
        
        // Отображаем заново
        DisplayItem(item, positions);
    }

    private void RenderAll()
    {
        Debug.Log("Render All");
        _view.Clear();

        foreach (Item item in _inventory)
            DisplayItem(item, _inventory.GetPositions(item));
    }

    private void DisplayItem(Item item, Vector2Int[] positions)
    {
        // Вычисляем границы
        (Vector2Int min, Vector2Int max) = CalculateBounds(positions);
        
        // Вычисляем размеры
        int cols = max.x - min.x + 1;
        int rows = max.y - min.y + 1;
        Vector2 containerSize = CalculateContainerSize(cols, rows);
        
        // Получаем позицию
        Vector2 containerPosition = _view.GetCellPosition(min);
        
        // Создаём контейнер
        InventoryItem inventoryItem = _view.CreateItemContainer(containerSize, containerPosition);
        _view.RegisterItemContainer(item, inventoryItem);
        
        // Устанавливаем иконку
        if (_itemCatalog.GetItemData(item.ItemID, out var data))
            _view.SetItemIcon(inventoryItem, data.Icon);
        
        // Заполняем клетки
        foreach (Vector2Int pos in positions)
        {
            Vector2Int matrixPosition = pos - min;
            _view.SetCellData(pos, item, inventoryItem, matrixPosition);
        }
    }

    private void ClearItemCells(Item item)
    {
        Vector2Int[] positions = _inventory.GetPositions(item);
        foreach (Vector2Int pos in positions)
            _view.ClearCell(pos);
    }

    private (Vector2Int min, Vector2Int max) CalculateBounds(Vector2Int[] positions)
    {
        Vector2Int min = positions[0];
        Vector2Int max = positions[0];

        foreach (Vector2Int p in positions)
        {
            if (p.x < min.x) min.x = p.x;
            if (p.y < min.y) min.y = p.y;
            if (p.x > max.x) max.x = p.x;
            if (p.y > max.y) max.y = p.y;
        }

        return (min, max);
    }

    private Vector2 CalculateContainerSize(int cols, int rows)
    {
        return new Vector2(
            cols * _view.CellSize.x + (cols - 1) * _view.Spacing.x,
            rows * _view.CellSize.y + (rows - 1) * _view.Spacing.y
        );
    }

    private void AddTestItems()
    {
        Item ring = new Item(ItemID.Ring.ToString(), 1, 1) { ItemID = ItemID.Ring };
        Item ar1 = new Item(ItemID.AR_01.ToString(), 4, 2) { ItemID = ItemID.AR_01 };
        Item ar2 = new Item(ItemID.AR_02.ToString(), 4, 2) { ItemID = ItemID.AR_02 };

        _inventory.AddItem(ring);
        _inventory.AddItem(ar1);
        _inventory.AddItem(ar2);
    }

    [Button]
    public void Reorganize()
    {
        _inventory.ReorganizeSpace();
        RenderAll();
    }

    private void OnDestroy()
    {
        if (_inventory != null)
            _inventory.OnMoved -= OnItemMoved;
    }
}