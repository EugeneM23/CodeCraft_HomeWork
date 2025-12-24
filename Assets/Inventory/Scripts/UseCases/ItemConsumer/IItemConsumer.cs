using Inventories;

public interface IItemConsumer
{
    abstract T GetComponent<T>();
    bool EquipWeapon(ItemInstance itemInstance);
    bool EquipArmor(ItemInstance itemInstance);
    void SetInventory(Inventory inventory);
    bool EquipAmmo(ItemInstance itemInstance);
}