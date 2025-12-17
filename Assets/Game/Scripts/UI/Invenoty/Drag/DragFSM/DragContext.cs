using Game.Scripts.UI.Equipment;
using Inventories;
using UnityEngine;

public class DragContext
{
    public DragItem CurrentDragItem { get; set; }
    public Inventory SourceInventory { get; set; }
    public Inventory CurrentInventory { get; set; }
    public Vector2Int GrabbedCell { get; set; }
    public Vector2Int CurrentDragCell { get; set; }

    public bool IsDragging => CurrentDragItem != null;
    public EquipmentSlot EquipmentSlot { get; set; }
    public Vector3 DragOffset { get; set; }

    public void Clear()
    {
        DragOffset = Vector3.zero;
        CurrentDragCell = Vector2Int.zero;
        EquipmentSlot = null;
        CurrentDragItem = null;
        SourceInventory = null;
        CurrentInventory = null;
        GrabbedCell = Vector2Int.zero;
    }
}