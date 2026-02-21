using Inventories;
using UnityEngine;

public class DragContext
{
    public Item Item { get; private set; }
    public Vector2Int StartPosition { get; private set; }
    public Vector2Int ClickOffset { get; private set; }
    public Vector2 DragOffset { get; private set; }
    public bool IsDragging { get; private set; }
    public InventoryCell CurrentInventoryCell { get; private set; }

    public void BeginDrag(Item item, Vector2Int startPosition, Vector2Int clickOffset, Vector2 dragOffset)
    {
        Item = item;
        StartPosition = startPosition;
        ClickOffset = clickOffset;
        DragOffset = dragOffset;
        IsDragging = true;
    }

    public void UpdateCurrentCell(InventoryCell inventoryCell)
    {
        CurrentInventoryCell = inventoryCell;
    }

    public void EndDrag()
    {
        Item = null;
        StartPosition = default;
        ClickOffset = default;
        DragOffset = default;
        IsDragging = false;
        CurrentInventoryCell = null;
    }

    public void Reset()
    {
        EndDrag();
    }
}