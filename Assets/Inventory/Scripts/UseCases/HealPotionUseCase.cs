using UnityEngine;
using Inventories;

[CreateAssetMenu(fileName = "HealPotionUseCase", menuName = "InventoryItem/UseCases/HealPotionUseCase")]
public class HealPotionUseCase : ItemUseCase
{
    [SerializeField] private int healAmount = 20;

    public override void Invoke(Inventory inventory, ItemInstance itemInstance)
    {
        IItemConsumer consumer = inventory.Owner;

        TestCharacter character = consumer.GetComponent<TestCharacter>();

        if (character != null)
        {
            character.Health += healAmount;
            inventory.RemoveItem(itemInstance.ID);
            Debug.Log($"Healed! New health: {character.Health}");
        }
    }
}