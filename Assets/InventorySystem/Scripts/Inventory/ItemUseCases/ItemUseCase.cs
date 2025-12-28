using UnityEngine;
using Inventories;

public abstract class ItemUseCase : ScriptableObject
{
    public abstract void Invoke(Inventory inventory, Item item);
    
    protected void ConsumeItem(Inventory inventory, Item item, int amount = 1)
    {
        bool stackIsEmpty = item.TryRemoveQuantity(amount);
        
        if (stackIsEmpty)
        {
            inventory.RemoveItem(item.ID);
        }
    }
}