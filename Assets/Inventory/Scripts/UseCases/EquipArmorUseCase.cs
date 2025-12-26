using Inventories;
using UnityEngine;

[CreateAssetMenu(fileName = "EquipArmorUseCase", menuName = "InventoryItem/UseCases/EquipArmorUseCase")]
public class EquipArmorUseCase : ItemUseCase
{
    public override void Invoke(Inventory inventory, ItemInstance itemInstance)
    {
        IItemConsumer consumer = inventory.Owner;
        if (consumer == null)
            return;

        inventory.RemoveItem(itemInstance.ID);
        consumer.Equip(itemInstance);
    }
}