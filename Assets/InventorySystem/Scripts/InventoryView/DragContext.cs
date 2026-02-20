using Inventories;
using UnityEngine;

public class DragContext
{
    public Item Item { get; private set; }
    public Vector2Int StartPosition { get; private set; }
    public Vector2Int ClickOffset { get; private set; }
    public Vector2 DragOffset { get; private set; }
    public bool IsDragging { get; private set; }
    public Cell CurrentCell { get; private set; }

    public void BeginDrag(Item item, Vector2Int startPosition, Vector2Int clickOffset, Vector2 dragOffset)
    {
        Item = item;
        StartPosition = startPosition;
        ClickOffset = clickOffset;
        DragOffset = dragOffset;
        IsDragging = true;
    }

    public void UpdateCurrentCell(Cell cell)
    {
        CurrentCell = cell;
    }

    public void EndDrag()
    {
        Item = null;
        StartPosition = default;
        ClickOffset = default;
        DragOffset = default;
        IsDragging = false;
        CurrentCell = null;
    }

    public void Reset()
    {
        EndDrag();
    }
}