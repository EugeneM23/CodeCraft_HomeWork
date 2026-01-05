using UnityEngine;
using Inventories;

[CreateAssetMenu(fileName = "EquipWeaponUseCase", menuName = "InventoryItem/UseCases/EquipWeaponUseCase")]
public class EquipUseCase : ItemUseCase
{
    public override void Invoke(InventoryPresenter presenter, Item item)
    {
        IItemConsumer consumer = presenter.Owner;
        if (consumer == null) return;

        presenter.RemoveItem(item.ID);

        consumer.Equip(item);
    }
}