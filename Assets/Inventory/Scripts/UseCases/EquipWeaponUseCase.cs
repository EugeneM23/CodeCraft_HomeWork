using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentWeaponUseCase", menuName = "InventoryItem/UseCases/EquipmentWeaponUseCase")]
public class EquipWeaponUseCase : ItemUseCase
{
    public override void Invoke(IItemConsumer itemConsumer)
    {
        Debug.Log("EquipItemUseCase");
    }
}