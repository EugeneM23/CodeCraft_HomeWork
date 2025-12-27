using UnityEngine;
using Inventories;

public abstract class ItemUseCase : ScriptableObject
{
    public abstract void Invoke(Inventory inventory, ItemInstance itemInstance);
    
    protected void ConsumeItem(Inventory inventory, ItemInstance itemInstance, int amount = 1)
    {
        bool stackIsEmpty = itemInstance.TryRemoveQuantity(amount);
        
        if (stackIsEmpty)
        {
            inventory.RemoveItem(itemInstance.ID);
        }
    }
}