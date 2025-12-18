using UnityEngine;
using Inventories;

[CreateAssetMenu(fileName = "EquipWeaponUseCase", menuName = "InventoryItem/UseCases/EquipWeaponUseCase")]
public class EquipWeaponUseCase : ItemUseCase
{
    public override void Invoke(Inventory inventory, ItemInstance itemInstance)
    {
        IItemConsumer consumer = inventory.Owner;

        if (consumer.EquipWeapon(itemInstance))
            inventory.RemoveItem(itemInstance.ID);
    }
}