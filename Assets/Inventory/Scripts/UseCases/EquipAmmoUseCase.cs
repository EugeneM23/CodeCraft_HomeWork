using Inventories;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipAmmoUseCase", menuName = "InventoryItem/UseCases/EquipAmmoUseCase")]
public class EquipAmmoUseCase : ItemUseCase
{
    public override void Invoke(Inventory inventory, ItemInstance itemInstance)
    {
        IItemConsumer consumer = inventory.Owner;
        if (consumer == null)
            return;

        inventory.RemoveItem(itemInstance.ID);
        consumer.EquipAmmo(itemInstance);
    }
}