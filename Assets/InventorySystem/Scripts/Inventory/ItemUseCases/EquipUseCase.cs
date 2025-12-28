using UnityEngine;
using Inventories;

[CreateAssetMenu(fileName = "EquipWeaponUseCase", menuName = "InventoryItem/UseCases/EquipWeaponUseCase")]
public class EquipUseCase : ItemUseCase
{
    public override void Invoke(Inventory inventory, Item item)
    {
        IItemConsumer consumer = inventory.Owner;
        if (consumer == null) return;

        inventory.RemoveItem(item.ID);

        consumer.Equip(item);
    }
}