using Inventories;

public interface IItemConsumer
{
    abstract T GetComponent<T>();
    Equipment Equipment { get; set; }
    bool EquipWeapon(ItemInstance itemInstance);
    bool EquipArmor(ItemInstance itemInstance);
    void SetInventory(Inventory inventory);
}