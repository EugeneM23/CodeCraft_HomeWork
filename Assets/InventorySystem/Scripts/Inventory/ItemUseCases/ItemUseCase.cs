using UnityEngine;
using Inventories;

public abstract class ItemUseCase : ScriptableObject
{
    public abstract void Invoke(InventoryPresenter presenter, Item item);
    
    protected void ConsumeItem(InventoryPresenter presenter, Item item, int amount = 1)
    {
        bool stackIsEmpty = item.TryRemoveQuantity(amount);
        
        if (stackIsEmpty)
        {
            presenter.RemoveItem(item.ID);
        }
    }
}