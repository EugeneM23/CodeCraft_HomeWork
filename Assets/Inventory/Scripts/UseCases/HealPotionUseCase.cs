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

        if (character == null)
        {
            Debug.LogWarning("Character not found!");
            return;
        }

        character.Health += healAmount;
        Debug.Log($"Healed! New health: {character.Health}");

        bool stackIsEmpty = itemInstance.UseOne();

        if (stackIsEmpty)
        {
            inventory.RemoveItem(itemInstance.ID);
            Debug.Log("Item stack depleted and removed");
        }
        else
        {
            Debug.Log($"Items remaining: {itemInstance.StackQuantity}");
        }
    }
}