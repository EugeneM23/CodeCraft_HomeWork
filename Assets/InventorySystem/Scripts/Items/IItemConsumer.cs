using Inventories;

public interface IItemConsumer
{
    abstract T GetComponent<T>();
    bool Equip(Item item);
    void SetInventory(InventoryPresenter presenter);
}