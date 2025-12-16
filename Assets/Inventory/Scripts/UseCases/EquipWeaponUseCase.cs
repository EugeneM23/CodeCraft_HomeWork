using UnityEngine;
using Inventories;

[CreateAssetMenu(fileName = "EquipWeaponUseCase", menuName = "InventoryItem/UseCases/EquipWeaponUseCase")]
public class EquipWeaponUseCase : ItemUseCase
{
    public override void Invoke(Inventory inventory, ItemInstance itemInstance)
    {
        IItemConsumer consumer = inventory.Owner;

        if (consumer == null)
        {
            Debug.LogWarning("Inventory has no owner!");
            return;
        }

        Debug.Log("Weapon equipped!");
        // Здесь логика экипировки
    }
}