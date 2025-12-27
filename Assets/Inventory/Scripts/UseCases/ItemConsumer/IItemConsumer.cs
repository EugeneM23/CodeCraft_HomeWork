using Inventories;

public interface IItemConsumer
{
    abstract T GetComponent<T>();
    bool Equip(ItemInstance itemInstance);
    void SetInventory(Inventory inventory);
}