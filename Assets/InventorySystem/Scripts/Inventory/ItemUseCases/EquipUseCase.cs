using UnityEngine;
using Inventories;

[CreateAssetMenu(fileName = "EquipWeaponUseCase", menuName = "InventoryItem/UseCases/EquipWeaponUseCase")]
public class EquipUseCase : ItemUseCase
{
    public override void Invoke(Inventory inventory, ItemInstance itemInstance)
    {
        IItemConsumer consumer = inventory.Owner;
        if (consumer == null) return;

        inventory.RemoveItem(itemInstance.ID);

        consumer.Equip(itemInstance);
    }
}