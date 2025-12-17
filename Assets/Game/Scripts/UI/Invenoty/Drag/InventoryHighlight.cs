using Inventories;
using UnityEngine;

public class InventoryHighlight : MonoBehaviour
{
    [SerializeField] private DragFSM _fsm;
    [SerializeField] private InventoryView _inventoryView;

    private void Update()
    {
        if (!_fsm.Context.IsDragging) return;
        
        _fsm.Context.CurrentDragCell
        
    }
}