using Equipment;
using Inventories;
using UnityEngine;
using UnityEngine.UI;

public class DragContext
{
    public Item Item { get; private set; }
    public Vector2Int StartPosition { get; private set; }
    public Vector2Int ClickOffset { get; private set; }
    public Vector2 DragOffset { get; private set; }
    public bool IsDragging { get; private set; }
    public CellView CurrentCellView { get; private set; }
    public CellView StartCellView { get; private set; }
    public EquipmentPresenter EquipmentPresenter { get; private set; }

    public void BeginDrag(Item item, Vector2Int startPosition, Vector2Int clickOffset, Vector2 dragOffset,
        CellView cellView = null, EquipmentPresenter equipmentPresenter = null)
    {
        Item = item;
        StartPosition = startPosition;
        ClickOffset = clickOffset;
        DragOffset = dragOffset;
        IsDragging = true;
        StartCellView = cellView;
        EquipmentPresenter = equipmentPresenter;
    }

    public void UpdateCurrentCell(CellView cellView)
    {
        CurrentCellView = cellView;
    }

    public void Reset()
    {
        Item = null;
        StartPosition = default;
        ClickOffset = default;
        DragOffset = default;
        IsDragging = false;
        CurrentCellView = null;
        StartCellView = null;
        EquipmentPresenter = null;
    }
}