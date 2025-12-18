using Inventories;

public interface IItemConsumer
{
    abstract T GetComponent<T>();
    Inventory Inventory { get; set; }
    bool EquipWeapon(ItemInstance itemInstance);
}